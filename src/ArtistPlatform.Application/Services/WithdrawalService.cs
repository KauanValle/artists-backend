using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
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

public interface IWithdrawalService
{
    Task<WithdrawalSummaryDto> GetArtistWithdrawalSummaryAsync();
}

public class WithdrawalService(
    IAppDbContext db,
    ICurrentUserService currentUser,
    ITaxaAdministrativaService taxaService) : IWithdrawalService
{
    public async Task<WithdrawalSummaryDto> GetArtistWithdrawalSummaryAsync()
    {
        var artistId = currentUser.EnsureArtist();
        var taxaPercentual = await taxaService.GetPercentualAsync();

        // Shows concluídos do artista com pagamento confirmado
        var completedShows = await (
            from b in db.BookingRequests.AsNoTracking()
            join e in db.Events.AsNoTracking() on b.EventId equals e.Id
            join p in db.ShowPayments.AsNoTracking() on b.Id equals p.BookingRequestId
            where b.ArtistId == artistId
               && (b.Status == BookingStatus.Completed || e.Status == EventStatus.Completed)
               && p.Status == ShowPaymentStatus.Paid
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
                s.Id,
                s.EventType.ToString(),
                s.EventDate,
                s.Location,
                s.Amount,
                taxaPercentual,
                taxaValor,
                valorLiquido,
                s.ConfirmedAt
            );
        }).ToList();

        var totalBruto = shows.Sum(s => s.ShowAmount);
        var totalTaxa = shows.Sum(s => s.TaxaValor);
        var totalLiquido = shows.Sum(s => s.ValorLiquido);

        return new WithdrawalSummaryDto(totalBruto, totalTaxa, totalLiquido, taxaPercentual, shows);
    }
}
