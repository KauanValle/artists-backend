using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public interface IBookingService
{
    Task<BookingDto> CreateAsync(CreateBookingRequestDto request);
    Task<PagedResult<BookingDto>> ListAsync(BookingStatus? status, int page, int pageSize);
    Task<BookingDto> GetAsync(Guid id);
    Task<BookingDto> StartNegotiationAsync(Guid id);
    Task<BookingDto> AcceptAsync(Guid id);
    Task<BookingDto> RejectAsync(Guid id);
    Task<BookingDto> CancelAsync(Guid id);
}

public class BookingService(
    IAppDbContext db,
    ICurrentUserService currentUser,
    IUserDirectory userDirectory,
    INotificationService notifications) : IBookingService
{
    public async Task<BookingDto> CreateAsync(CreateBookingRequestDto request)
    {
        var contractorId = currentUser.EnsureContractor();

        if (request.EndTime <= request.StartTime)
            throw new ValidationException("O horário final deve ser maior que o inicial.");
        if (request.EventDate < DateOnly.FromDateTime(DateTime.Today))
            throw new ValidationException("A data do evento não pode estar no passado.");

        var artist = await db.Artists.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.ArtistId)
            ?? throw new NotFoundException("Artista não encontrado.");
        var artistUser = await db.Artists.AsNoTracking().Where(a => a.Id == request.ArtistId).Select(a => a.UserId).FirstAsync();

        var booking = new BookingRequest
        {
            ArtistId = request.ArtistId,
            ContractorId = contractorId,
            EventDate = request.EventDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Location = request.Location,
            EventType = request.EventType,
            EstimatedAudience = request.EstimatedAudience,
            Budget = request.Budget,
            Message = request.Message ?? string.Empty,
            Status = BookingStatus.Requested
        };
        db.BookingRequests.Add(booking);

        var contractorUserId = currentUser.UserId!.Value;
        db.Conversations.Add(new Conversation
        {
            BookingRequestId = booking.Id,
            ArtistUserId = artistUser,
            ContractorUserId = contractorUserId
        });

        await db.SaveChangesAsync();

        await notifications.NotifyAsync(
            artistUser,
            NotificationType.NewBookingRequest,
            "Nova solicitação de contratação",
            $"{artist.ArtisticName} recebeu uma nova solicitação para {request.EventDate:dd/MM/yyyy} às {request.StartTime}.",
            $"/shows/{booking.Id}");

        return await ToDtoAsync(booking, artist.ArtisticName, await GetContractorNameAsync(contractorId));
    }

    /// <summary>Lista com abas por status (PRD §10), filtrada pelo papel do usuário autenticado.</summary>
    public async Task<PagedResult<BookingDto>> ListAsync(BookingStatus? status, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = db.BookingRequests.AsNoTracking();

        if (currentUser.Role == UserRole.Artist)
            query = query.Where(b => b.ArtistId == currentUser.ArtistId);
        else if (currentUser.Role == UserRole.Contractor)
            query = query.Where(b => b.ContractorId == currentUser.ContractorId);
        else
            currentUser.EnsureAuthenticated();

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        var total = await query.CountAsync();
        var bookings = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<BookingDto>(await MapAllAsync(bookings), page, pageSize, total);
    }

    public async Task<BookingDto> GetAsync(Guid id)
    {
        var booking = await EnsureAccessAsync(id);
        return (await MapAllAsync([booking])).First();
    }

    /// <summary>Artista inicia a negociação (REQUESTED → NEGOTIATING).</summary>
    public async Task<BookingDto> StartNegotiationAsync(Guid id)
    {
        var booking = await EnsureArtistOwnerAsync(id);
        if (booking.Status != BookingStatus.Requested)
            throw new BusinessRuleException("Só é possível iniciar negociação a partir de uma solicitação nova.");

        booking.Status = BookingStatus.Negotiating;
        booking.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return (await MapAllAsync([booking])).First();
    }

    /// <summary>Artista recusa a solicitação (RN018: recusada não volta a ser aceita).</summary>
    public async Task<BookingDto> RejectAsync(Guid id)
    {
        var booking = await EnsureArtistOwnerAsync(id);
        if (!booking.CanEvolve)
            throw new BusinessRuleException("RN018: esta solicitação não pode mais ser recusada.");

        var artist = await db.Artists.AsNoTracking().FirstAsync(a => a.Id == booking.ArtistId);
        var contractorUser = await db.Contractors.AsNoTracking().Where(c => c.Id == booking.ContractorId).Select(c => c.UserId).FirstAsync();

        booking.Status = BookingStatus.Rejected;
        booking.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await notifications.NotifyAsync(contractorUser, NotificationType.ProposalRejected,
            "Solicitação recusada",
            $"{artist.ArtisticName} recusou a solicitação para {booking.EventDate:dd/MM/yyyy}.",
            $"/solicitacoes/{booking.Id}");

        return (await MapAllAsync([booking])).First();
    }

    public async Task<BookingDto> CancelAsync(Guid id)
    {
        var booking = await EnsureAccessAsync(id);
        if (booking.Status is BookingStatus.Confirmed or BookingStatus.Completed)
            throw new BusinessRuleException("Eventos confirmados devem ser cancelados pela agenda.");
        if (!booking.CanEvolve)
            throw new BusinessRuleException("Esta solicitação não pode mais ser cancelada.");

        booking.Status = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return (await MapAllAsync([booking])).First();
    }

    /// <summary>
    /// Aceite direto pelo artista, sem proposta formal: usa o orçamento informado pelo contratante
    /// como valor final e dispara os mesmos efeitos do aceite de proposta (PRD §11 — RN007/RN008/RN010).
    /// </summary>
    public async Task<BookingDto> AcceptAsync(Guid id)
    {
        var booking = await EnsureArtistOwnerAsync(id);

        if (booking.Status is not (BookingStatus.Requested or BookingStatus.Negotiating))
            throw new BusinessRuleException("O aceite direto está disponível apenas para solicitações novas ou em negociação.");

        var hasPendingProposal = await db.Proposals.AsNoTracking()
            .AnyAsync(p => p.BookingRequestId == booking.Id && p.Status == ProposalStatus.Pending);
        if (hasPendingProposal)
            throw new BusinessRuleException("Já existe uma proposta pendente para esta solicitação. Aceite ou recuse a proposta.");

        if (booking.Budget is not > 0)
            throw new BusinessRuleException("Esta solicitação não tem orçamento informado. Envie uma proposta para definir o valor.");

        var interval = booking.ToInterval();

        // RN017/RN009: a agenda não pode conflitar
        var hasEventConflict = await db.Events.AsNoTracking()
            .AnyAsync(e => e.ArtistId == booking.ArtistId &&
                           (e.Status == EventStatus.Scheduled || e.Status == EventStatus.Confirmed) &&
                           e.StartDateTime < interval.End && e.EndDateTime > interval.Start);
        if (hasEventConflict)
            throw new BusinessRuleException("RN017: você já possui um evento confirmado nesse horário.");

        var blockedRows = await db.ArtistAvailabilities.AsNoTracking()
            .Where(a => a.ArtistId == booking.ArtistId && a.Status != AvailabilityStatus.Available &&
                        a.Date == booking.EventDate)
            .ToListAsync();
        foreach (var row in blockedRows)
        {
            var blockedInterval = new Interval(
                row.Date.ToDateTime(row.StartTime),
                row.Date.ToDateTime(row.IsAllDay ? new TimeOnly(23, 59, 59) : row.EndTime));
            if (blockedInterval.Overlaps(interval))
                throw new BusinessRuleException("RN017: o horário está bloqueado na sua agenda.");
        }

        var artist = await db.Artists.AsNoTracking().FirstAsync(a => a.Id == booking.ArtistId);
        var contractorUser = await db.Contractors.AsNoTracking()
            .Where(c => c.Id == booking.ContractorId).Select(c => c.UserId).FirstAsync();

        // RN007: aceite cria/confirma o evento (RN008: bloqueia a agenda)
        var ev = new Event
        {
            ArtistId = booking.ArtistId,
            ContractorId = booking.ContractorId,
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

        booking.EventId = ev.Id;
        booking.Status = BookingStatus.Confirmed;
        booking.UpdatedAt = DateTime.UtcNow;

        // RN010: receita PENDING com o orçamento acordado
        db.FinancialTransactions.Add(new FinancialTransaction
        {
            ArtistId = booking.ArtistId,
            Type = TransactionType.Income,
            Category = "Show",
            Amount = booking.Budget.Value,
            DueDate = booking.EventDate,
            Status = TransactionStatus.Pending,
            EventId = ev.Id,
            Notes = $"Receita gerada pelo aceite direto da solicitação (contratação {booking.Id})."
        });

        await db.SaveChangesAsync();

        await notifications.NotifyAsync(artist.UserId, NotificationType.ShowConfirmed,
            "Show confirmado",
            $"Solicitação aceita! Show confirmado para {booking.EventDate:dd/MM/yyyy} às {booking.StartTime} em {booking.Location}.",
            $"/shows/{booking.Id}");
        await notifications.NotifyAsync(contractorUser, NotificationType.ShowConfirmed,
            "Solicitação aceita",
            $"{artist.ArtisticName} aceitou sua solicitação de {booking.Budget.Value:C}. O evento está confirmado.",
            $"/solicitacoes/{booking.Id}");

        return (await MapAllAsync([booking])).First();
    }

    private async Task<BookingRequest> EnsureArtistOwnerAsync(Guid id)
    {
        var booking = await db.BookingRequests.FirstOrDefaultAsync(b => b.Id == id)
            ?? throw new NotFoundException("Solicitação não encontrada.");
        if (booking.ArtistId != currentUser.ArtistId && currentUser.Role != UserRole.Admin)
            throw new ForbiddenException("Apenas o artista responsável pode executar esta ação.");
        return booking;
    }

    private async Task<BookingRequest> EnsureAccessAsync(Guid id)
    {
        var booking = await db.BookingRequests.FirstOrDefaultAsync(b => b.Id == id)
            ?? throw new NotFoundException("Solicitação não encontrada.");

        var allowed =
            (currentUser.Role == UserRole.Artist && booking.ArtistId == currentUser.ArtistId) ||
            (currentUser.Role == UserRole.Contractor && booking.ContractorId == currentUser.ContractorId) ||
            currentUser.Role == UserRole.Admin;
        if (!allowed)
            throw new ForbiddenException();
        return booking;
    }

    private async Task<List<BookingDto>> MapAllAsync(List<BookingRequest> bookings)
    {
        if (bookings.Count == 0) return [];

        var artistIds = bookings.Select(b => b.ArtistId).Distinct().ToList();
        var contractorIds = bookings.Select(b => b.ContractorId).Distinct().ToList();
        var bookingIds = bookings.Select(b => b.Id).ToList();

        var artists = await db.Artists.AsNoTracking()
            .Where(a => artistIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => new { a.ArtisticName, a.ProfilePhotoUrl });
        var contractors = await db.Contractors.AsNoTracking()
            .Where(c => contractorIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);
        var conversations = await db.Conversations.AsNoTracking()
            .Where(c => bookingIds.Contains(c.BookingRequestId))
            .ToDictionaryAsync(c => c.BookingRequestId, c => c.Id);

        return bookings.Select(b => new BookingDto(
            b.Id,
            b.ArtistId,
            artists.GetValueOrDefault(b.ArtistId)?.ArtisticName ?? "Artista",
            artists.GetValueOrDefault(b.ArtistId)?.ProfilePhotoUrl,
            b.ContractorId,
            contractors.GetValueOrDefault(b.ContractorId, "Contratante"),
            b.EventDate,
            b.StartTime,
            b.EndTime,
            b.Location,
            b.EventType,
            b.EstimatedAudience,
            b.Budget,
            b.Message,
            b.Status,
            b.EventId,
            b.AcceptedProposalId,
            conversations.GetValueOrDefault(b.Id, Guid.Empty),
            b.CreatedAt)).ToList();
    }

    private async Task<string> GetContractorNameAsync(Guid contractorId)
    {
        return await db.Contractors.AsNoTracking()
            .Where(c => c.Id == contractorId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync() ?? "Contratante";
    }

    private async Task<BookingDto> ToDtoAsync(BookingRequest booking, string artistName, string contractorName)
    {
        var conversation = await db.Conversations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.BookingRequestId == booking.Id);

        return new BookingDto(
            booking.Id, booking.ArtistId, artistName, null, booking.ContractorId, contractorName,
            booking.EventDate, booking.StartTime, booking.EndTime, booking.Location, booking.EventType,
            booking.EstimatedAudience, booking.Budget, booking.Message, booking.Status,
            booking.EventId, booking.AcceptedProposalId, conversation?.Id ?? Guid.Empty, booking.CreatedAt);
    }
}
