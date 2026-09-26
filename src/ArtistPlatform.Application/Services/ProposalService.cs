using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public interface IProposalService
{
    Task<ProposalDto> CreateAsync(CreateProposalRequestDto request);
    Task<ProposalDto> GetAsync(Guid id);
    Task<List<ProposalDto>> ListForBookingAsync(Guid bookingId);
    Task<ProposalDto> AcceptAsync(Guid id);
    Task<ProposalDto> RejectAsync(Guid id);
}

/// <summary>
/// Ciclo da contratação (PRD §11):
/// aceitar proposta ⇒ proposta ACCEPTED, solicitação ACCEPTED/CONFIRMED, evento CONFIRMADO criado,
/// agenda bloqueada (o evento bloqueia), receita PENDING criada e ambas as partes notificadas
/// (RN007, RN008, RN010).
/// </summary>
public class ProposalService(
    IAppDbContext db,
    ICurrentUserService currentUser,
    IDateTimeClock clock,
    INotificationService notifications) : IProposalService
{
    public async Task<ProposalDto> CreateAsync(CreateProposalRequestDto request)
    {
        var artistId = currentUser.EnsureArtist();

        if (request.Items is null || request.Items.Count == 0)
            throw new ValidationException("Inclua ao menos um item/serviço na proposta.");
        if (request.Discount < 0)
            throw new ValidationException("O desconto não pode ser negativo.");

        var booking = await db.BookingRequests.FirstOrDefaultAsync(b => b.Id == request.BookingRequestId)
            ?? throw new NotFoundException("Solicitação não encontrada.");
        if (booking.ArtistId != artistId && currentUser.Role != UserRole.Admin)
            throw new ForbiddenException("Apenas o artista responsável pode enviar a proposta.");
        if (!booking.CanEvolve)
            throw new BusinessRuleException("RN018: esta solicitação não aceita mais propostas.");

        var artistUser = await db.Artists.AsNoTracking().Where(a => a.Id == artistId).Select(a => a.UserId).FirstAsync();
        var contractorUser = await db.Contractors.AsNoTracking().Where(c => c.Id == booking.ContractorId).Select(c => c.UserId).FirstAsync();

        var proposal = new Proposal
        {
            BookingRequestId = booking.Id,
            ArtistId = artistId,
            ContractorId = booking.ContractorId,
            Status = ProposalStatus.Pending,
            TravelCost = request.TravelCost,
            EquipmentCost = request.EquipmentCost,
            Discount = request.Discount,
            ValidUntil = clock.UtcNow.AddDays(request.ValidityDays <= 0 ? 7 : request.ValidityDays),
            Notes = request.Notes ?? string.Empty,
            Items = request.Items.Select(i => new ProposalItem
            {
                Description = i.Description,
                Amount = i.Amount
            }).ToList()
        };
        proposal.FinalAmount = proposal.CalculateFinalAmount();
        if (proposal.FinalAmount < 0)
            throw new ValidationException("O valor final não pode ser negativo.");

        db.Proposals.Add(proposal);

        booking.Status = BookingStatus.Proposed;
        booking.UpdatedAt = clock.UtcNow;

        await db.SaveChangesAsync();

        var artist = await db.Artists.AsNoTracking().FirstAsync(a => a.Id == artistId);
        await notifications.NotifyAsync(contractorUser, NotificationType.NewProposal,
            "Nova proposta recebida",
            $"{artist.ArtisticName} enviou uma proposta de {proposal.FinalAmount:C} para {booking.EventDate:dd/MM/yyyy}.",
            $"/solicitacoes/{booking.Id}");

        return ToDto(proposal);
    }

    public async Task<ProposalDto> GetAsync(Guid id)
    {
        var proposal = await db.Proposals.AsNoTracking().Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException("Proposta não encontrada.");
        await EnsureAccessAsync(proposal);
        return ToDto(proposal);
    }

    public async Task<List<ProposalDto>> ListForBookingAsync(Guid bookingId)
    {
        var booking = await db.BookingRequests.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new NotFoundException("Solicitação não encontrada.");
        if (booking.ArtistId != currentUser.ArtistId && booking.ContractorId != currentUser.ContractorId && currentUser.Role != UserRole.Admin)
            throw new ForbiddenException();

        var proposals = await db.Proposals.AsNoTracking()
            .Include(p => p.Items)
            .Where(p => p.BookingRequestId == bookingId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
        return proposals.Select(ToDto).ToList();
    }

    /// <summary>Aceite pelo contratante: dispara todo o efeito do ciclo (PRD §11).</summary>
    public async Task<ProposalDto> AcceptAsync(Guid id)
    {
        var proposal = await db.Proposals.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException("Proposta não encontrada.");
        if (proposal.ContractorId != currentUser.ContractorId && currentUser.Role != UserRole.Admin)
            throw new ForbiddenException("Apenas o contratante pode aceitar a proposta.");

        var booking = await db.BookingRequests.FirstAsync(b => b.Id == proposal.BookingRequestId);

        // RN019: proposta expirada não pode ser aceita
        if (proposal.Status == ProposalStatus.Pending && clock.UtcNow > proposal.ValidUntil)
        {
            proposal.Status = ProposalStatus.Expired;
            booking.Status = BookingStatus.Expired;
            await db.SaveChangesAsync();
            throw new BusinessRuleException("RN019: esta proposta expirou e não pode mais ser aceita.");
        }
        if (!proposal.CanBeAccepted)
            throw new BusinessRuleException("Somente propostas pendentes podem ser aceitas.");

        // RN018: solicitação rejeitada/cancelada não volta a ser aceita
        if (!booking.CanEvolve)
            throw new BusinessRuleException("RN018: a solicitação não está mais em negociação.");

        var interval = booking.ToInterval();

        // RN017: não aceitar proposta conflitante
        var hasConflict = await db.Events.AsNoTracking()
            .AnyAsync(e => e.ArtistId == proposal.ArtistId &&
                           (e.Status == EventStatus.Scheduled || e.Status == EventStatus.Confirmed) &&
                           e.StartDateTime < interval.End && e.EndDateTime > interval.Start);
        if (hasConflict)
            throw new BusinessRuleException("RN017: o artista já possui um evento confirmado nesse horário.");

        var blocked = await db.ArtistAvailabilities.AsNoTracking()
            .Where(a => a.ArtistId == proposal.ArtistId && a.Status != AvailabilityStatus.Available &&
                        a.Date == booking.EventDate)
            .ToListAsync();
        foreach (var row in blocked)
        {
            var ri = new Interval(
                row.Date.ToDateTime(row.StartTime),
                row.Date.ToDateTime(row.IsAllDay ? new TimeOnly(23, 59, 59) : row.EndTime));
            if (ri.Overlaps(interval))
                throw new BusinessRuleException("RN017: o horário está bloqueado na agenda do artista.");
        }

        var artist = await db.Artists.AsNoTracking().FirstAsync(a => a.Id == proposal.ArtistId);
        var contractorUserId = await db.Contractors.AsNoTracking()
            .Where(c => c.Id == proposal.ContractorId).Select(c => c.UserId).FirstAsync();

        // 1) proposta ACCEPTED
        proposal.Status = ProposalStatus.Accepted;
        proposal.RespondedAt = clock.UtcNow;

        // 2) evento criado/confirmado (RN007) — bloqueia a agenda (RN008)
        var ev = new Event
        {
            ArtistId = proposal.ArtistId,
            ContractorId = proposal.ContractorId,
            BookingRequestId = booking.Id,
            Title = $"{artist.ArtisticName} — {booking.EventType}",
            Type = booking.EventType,
            StartDateTime = interval.Start,
            EndDateTime = interval.End,
            Location = booking.Location,
            Description = booking.Message,
            Status = EventStatus.Confirmed
        };
        db.Events.Add(ev);

        // 3) solicitação ACCEPTED → CONFIRMED
        booking.AcceptedProposalId = proposal.Id;
        booking.EventId = ev.Id;
        booking.Status = BookingStatus.Confirmed;
        booking.UpdatedAt = clock.UtcNow;

        // 4) receita PENDING (RN010)
        var income = new FinancialTransaction
        {
            ArtistId = proposal.ArtistId,
            Type = TransactionType.Income,
            Category = "Show",
            Amount = proposal.FinalAmount,
            DueDate = booking.EventDate,
            Status = TransactionStatus.Pending,
            EventId = ev.Id,
            Notes = $"Receita gerada pelo aceite da proposta (contratação {booking.Id})."
        };
        db.FinancialTransactions.Add(income);

        await db.SaveChangesAsync();

        // 5) notificações para artista e contratante
        await notifications.NotifyAsync(artist.UserId, NotificationType.ShowConfirmed,
            "Show confirmado",
            $"Proposta aceita! Show confirmado para {booking.EventDate:dd/MM/yyyy} às {booking.StartTime} em {booking.Location}.",
            $"/shows/{booking.Id}");
        await notifications.NotifyAsync(contractorUserId, NotificationType.ProposalAccepted,
            "Proposta aceita",
            $"Sua proposta de {proposal.FinalAmount:C} foi aceita. O evento está confirmado.",
            $"/solicitacoes/{booking.Id}");

        return ToDto(proposal);
    }

    public async Task<ProposalDto> RejectAsync(Guid id)
    {
        var proposal = await db.Proposals.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException("Proposta não encontrada.");
        if (proposal.ContractorId != currentUser.ContractorId && currentUser.Role != UserRole.Admin)
            throw new ForbiddenException("Apenas o contratante pode recusar a proposta.");
        if (proposal.Status != ProposalStatus.Pending)
            throw new BusinessRuleException("Somente propostas pendentes podem ser recusadas.");

        proposal.Status = ProposalStatus.Rejected;
        proposal.RespondedAt = clock.UtcNow;

        var booking = await db.BookingRequests.FirstAsync(b => b.Id == proposal.BookingRequestId);
        if (booking.Status == BookingStatus.Proposed)
            booking.Status = BookingStatus.Negotiating;
        booking.UpdatedAt = clock.UtcNow;

        await db.SaveChangesAsync();

        var artistUser = await db.Artists.AsNoTracking().Where(a => a.Id == proposal.ArtistId).Select(a => a.UserId).FirstAsync();
        await notifications.NotifyAsync(artistUser, NotificationType.ProposalRejected,
            "Proposta recusada",
            "O contratante recusou sua proposta. A negociação continua aberta.",
            $"/shows/{booking.Id}");

        return ToDto(proposal);
    }

    private async Task EnsureAccessAsync(Proposal proposal)
    {
        var allowed =
            (currentUser.Role == UserRole.Artist && proposal.ArtistId == currentUser.ArtistId) ||
            (currentUser.Role == UserRole.Contractor && proposal.ContractorId == currentUser.ContractorId) ||
            currentUser.Role == UserRole.Admin;
        if (!allowed)
            throw new ForbiddenException();
        await Task.CompletedTask;
    }

    private static ProposalDto ToDto(Proposal p) => new(
        p.Id, p.BookingRequestId, p.ArtistId, p.ContractorId, p.Status,
        p.Items.Select(i => new ProposalItemDto(i.Id, i.Description, i.Amount)).ToList(),
        p.TravelCost, p.EquipmentCost, p.Discount, p.FinalAmount,
        p.ValidUntil, p.Notes, p.RespondedAt, p.CreatedAt);
}
