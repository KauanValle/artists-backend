using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public record WithdrawalShowDto(
    Guid BookingId,
    string EventType,
    DateOnly EventDate,
    string Location,
    decimal ShowAmount,
    decimal TaxaPercentual,
    decimal TaxaValor,
    decimal ValorLiquido,
    DateTime? ConfirmedAt
);

public record WithdrawalSummaryDto(
    decimal TotalBruto,
    decimal TotalTaxa,
    decimal TotalLiquido,
    decimal TaxaPercentual,
    List<WithdrawalShowDto> Shows
);

public record WithdrawalRequestDto(
    Guid Id,
    Guid ArtistId,
    string ArtistName,
    decimal Amount,
    decimal TaxaPercentual,
    decimal TaxaValor,
    decimal ValorBruto,
    string Status,
    string? AdminNotes,
    DateTime CreatedAt,
    DateTime? ProcessedAt
);

public record CreateWithdrawalResultDto(Guid Id, string Message);

public interface IWithdrawalService
{
    Task<WithdrawalSummaryDto> GetArtistWithdrawalSummaryAsync();
    Task<CreateWithdrawalResultDto> RequestWithdrawalAsync();
    Task<List<WithdrawalRequestDto>> ListMyRequestsAsync();
    Task<List<WithdrawalRequestDto>> ListAllRequestsAsync();
    Task<WithdrawalRequestDto> ProcessRequestAsync(Guid requestId, bool approve, string? notes);
}

public class WithdrawalService(
    IAppDbContext db,
    ICurrentUserService currentUser,
    ITaxaAdministrativaService taxaService,
    INotificationService notifications) : IWithdrawalService
{
    public async Task<WithdrawalSummaryDto> GetArtistWithdrawalSummaryAsync()
    {
        var artistId = currentUser.EnsureArtist();
        var taxaPercentual = await taxaService.GetPercentualAsync();

        var completedShows = await (
            from b in db.BookingRequests.AsNoTracking()
            join e in db.Events.AsNoTracking() on b.EventId equals e.Id
            join p in db.ShowPayments.AsNoTracking() on b.Id equals p.BookingRequestId
            where b.ArtistId == artistId
               && (b.Status == BookingStatus.Completed || e.Status == EventStatus.Completed)
               && p.Status == ShowPaymentStatus.Paid
               && p.WithdrawnAt == null
            select new
            {
                b.Id,
                b.EventType,
                b.EventDate,
                b.Location,
                p.Amount,
                p.ConfirmedAt
            }
        ).OrderByDescending(x => x.EventDate).ToListAsync();

        var shows = completedShows.Select(s =>
        {
            var taxaValor = Math.Round(s.Amount * taxaPercentual / 100m, 2);
            var valorLiquido = s.Amount - taxaValor;
            return new WithdrawalShowDto(
                s.Id, s.EventType.ToString(), s.EventDate, s.Location,
                s.Amount, taxaPercentual, taxaValor, valorLiquido, s.ConfirmedAt
            );
        }).ToList();

        return new WithdrawalSummaryDto(
            shows.Sum(s => s.ShowAmount),
            shows.Sum(s => s.TaxaValor),
            shows.Sum(s => s.ValorLiquido),
            taxaPercentual,
            shows
        );
    }

    public async Task<CreateWithdrawalResultDto> RequestWithdrawalAsync()
    {
        var artistId = currentUser.EnsureArtist();

        // Verifica se já tem solicitação pendente
        var pendente = await db.WithdrawalRequests.AsNoTracking()
            .AnyAsync(w => w.ArtistId == artistId && w.Status == WithdrawalStatus.Pending);
        if (pendente)
            throw new BusinessRuleException("Você já possui uma solicitação de saque pendente.");

        var summary = await GetArtistWithdrawalSummaryAsync();
        if (summary.TotalLiquido <= 0)
            throw new BusinessRuleException("Não há valor disponível para saque.");

        var request = new WithdrawalRequest
        {
            ArtistId = artistId,
            Amount = summary.TotalLiquido,
            TaxaPercentual = summary.TaxaPercentual,
            TaxaValor = summary.TotalTaxa,
            ValorBruto = summary.TotalBruto,
            Status = WithdrawalStatus.Pending
        };
        db.WithdrawalRequests.Add(request);
        await db.SaveChangesAsync();

        return new CreateWithdrawalResultDto(request.Id, "Solicitação de saque enviada com sucesso!");
    }

    public async Task<List<WithdrawalRequestDto>> ListMyRequestsAsync()
    {
        var artistId = currentUser.EnsureArtist();
        var artistName = await db.Artists.AsNoTracking()
            .Where(a => a.Id == artistId).Select(a => a.ArtisticName).FirstAsync();

        return await db.WithdrawalRequests.AsNoTracking()
            .Where(w => w.ArtistId == artistId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WithdrawalRequestDto(
                w.Id, w.ArtistId, artistName, w.Amount,
                w.TaxaPercentual, w.TaxaValor, w.ValorBruto,
                w.Status.ToString(), w.AdminNotes,
                w.CreatedAt, w.ProcessedAt
            ))
            .ToListAsync();
    }

    public async Task<List<WithdrawalRequestDto>> ListAllRequestsAsync()
    {
        if (!currentUser.IsInRole(UserRole.Admin))
            throw new ForbiddenException("Acesso restrito a administradores.");

        return await (
            from w in db.WithdrawalRequests.AsNoTracking()
            join a in db.Artists.AsNoTracking() on w.ArtistId equals a.Id
            orderby w.CreatedAt descending
            select new WithdrawalRequestDto(
                w.Id, w.ArtistId, a.ArtisticName, w.Amount,
                w.TaxaPercentual, w.TaxaValor, w.ValorBruto,
                w.Status.ToString(), w.AdminNotes,
                w.CreatedAt, w.ProcessedAt
            )
        ).ToListAsync();
    }

    public async Task<WithdrawalRequestDto> ProcessRequestAsync(Guid requestId, bool approve, string? notes)
    {
        if (!currentUser.IsInRole(UserRole.Admin))
            throw new ForbiddenException("Acesso restrito a administradores.");

        var request = await db.WithdrawalRequests.FirstOrDefaultAsync(w => w.Id == requestId)
            ?? throw new NotFoundException("Solicitação de saque não encontrada.");

        if (request.Status != WithdrawalStatus.Pending)
            throw new BusinessRuleException("Esta solicitação já foi processada.");

        request.Status = approve ? WithdrawalStatus.Approved : WithdrawalStatus.Rejected;
        request.AdminNotes = notes;
        request.ProcessedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        // Quando aprovado, marca todos os ShowPayments concluídos do artista como sacados
        if (approve)
        {
            var completedPayments = await (
                from b in db.BookingRequests
                join e in db.Events on b.EventId equals e.Id
                join p in db.ShowPayments on b.Id equals p.BookingRequestId
                where b.ArtistId == request.ArtistId
                   && (b.Status == BookingStatus.Completed || e.Status == EventStatus.Completed)
                   && p.Status == ShowPaymentStatus.Paid
                   && p.WithdrawnAt == null
                select p
            ).ToListAsync();

            foreach (var p in completedPayments)
                p.WithdrawnAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        // Notifica o artista
        var artistUserId = await db.Artists.AsNoTracking()
            .Where(a => a.Id == request.ArtistId).Select(a => a.UserId).FirstAsync();
        var artistName = await db.Artists.AsNoTracking()
            .Where(a => a.Id == request.ArtistId).Select(a => a.ArtisticName).FirstAsync();

        var statusText = approve ? "aprovada" : "rejeitada";
        await notifications.NotifyAsync(artistUserId, NotificationType.WithdrawalProcessed,
            $"Solicitação de saque {statusText}",
            $"Sua solicitação de saque de {request.Amount:C} foi {statusText}.{(notes != null ? $" Obs: {notes}" : "")}",
            "/artist/saque");

        return new WithdrawalRequestDto(
            request.Id, request.ArtistId, artistName, request.Amount,
            request.TaxaPercentual, request.TaxaValor, request.ValorBruto,
            request.Status.ToString(), request.AdminNotes,
            request.CreatedAt, request.ProcessedAt
        );
    }
}
