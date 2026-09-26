using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public interface IFinancialService
{
    Task<PagedResult<TransactionDto>> ListAsync(TransactionType? type, TransactionStatus? status, DateOnly? from, DateOnly? to, int page, int pageSize);
    Task<TransactionDto> CreateAsync(CreateTransactionRequestDto request);
    Task<TransactionDto> UpdateAsync(Guid id, UpdateTransactionRequestDto request);
    Task DeleteAsync(Guid id);
    Task<TransactionDto> MarkSettledAsync(Guid id);
    Task<FinancialSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to);
}

public class FinancialService(IAppDbContext db, ICurrentUserService currentUser, IDateTimeClock clock) : IFinancialService
{
    public async Task<PagedResult<TransactionDto>> ListAsync(TransactionType? type, TransactionStatus? status, DateOnly? from, DateOnly? to, int page, int pageSize)
    {
        var artistId = currentUser.EnsureArtist();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.FinancialTransactions.AsNoTracking().Where(t => t.ArtistId == artistId);
        if (type.HasValue) query = query.Where(t => t.Type == type.Value);
        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        if (from.HasValue) query = query.Where(t => t.DueDate >= from.Value);
        if (to.HasValue) query = query.Where(t => t.DueDate <= to.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.DueDate ?? DateOnly.FromDateTime(t.CreatedAt))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var eventIds = items.Where(t => t.EventId.HasValue).Select(t => t.EventId!.Value).Distinct().ToList();
        var eventTitles = await db.Events.AsNoTracking()
            .Where(e => eventIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Title);

        return new PagedResult<TransactionDto>(
            items.Select(t => ToDto(t, eventTitles.GetValueOrDefault(t.EventId ?? Guid.Empty)))
                .ToList(),
            page, pageSize, total);
    }

    public async Task<TransactionDto> CreateAsync(CreateTransactionRequestDto request)
    {
        var artistId = currentUser.EnsureArtist();
        ValidateAmount(request.Amount);

        var tx = new FinancialTransaction
        {
            ArtistId = artistId,
            Type = request.Type,
            Category = request.Category.Trim(),
            Amount = request.Amount,
            DueDate = request.DueDate,
            Status = TransactionStatus.Pending,
            EventId = request.EventId,
            Notes = request.Notes ?? string.Empty
        };
        db.FinancialTransactions.Add(tx);
        await db.SaveChangesAsync();

        return ToDto(tx, null);
    }

    public async Task<TransactionDto> UpdateAsync(Guid id, UpdateTransactionRequestDto request)
    {
        var artistId = currentUser.EnsureArtist();
        var tx = await db.FinancialTransactions.FirstOrDefaultAsync(t => t.Id == id && t.ArtistId == artistId)
            ?? throw new NotFoundException("Transação não encontrada.");

        ValidateAmount(request.Amount);
        tx.Category = request.Category.Trim();
        tx.Amount = request.Amount;
        tx.DueDate = request.DueDate;
        tx.Status = request.Status;
        tx.EventId = request.EventId;
        tx.Notes = request.Notes ?? string.Empty;
        tx.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync();

        return ToDto(tx, null);
    }

    public async Task DeleteAsync(Guid id)
    {
        var artistId = currentUser.EnsureArtist();
        var tx = await db.FinancialTransactions.FirstOrDefaultAsync(t => t.Id == id && t.ArtistId == artistId)
            ?? throw new NotFoundException("Transação não encontrada.");
        db.FinancialTransactions.Remove(tx);
        await db.SaveChangesAsync();
    }

    /// <summary>Receita marcada como recebida / despesa marcada como paga.</summary>
    public async Task<TransactionDto> MarkSettledAsync(Guid id)
    {
        var artistId = currentUser.EnsureArtist();
        var tx = await db.FinancialTransactions.FirstOrDefaultAsync(t => t.Id == id && t.ArtistId == artistId)
            ?? throw new NotFoundException("Transação não encontrada.");

        tx.Status = tx.Type == TransactionType.Income ? TransactionStatus.Received : TransactionStatus.Paid;
        tx.SettlementDate = clock.Today;
        tx.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync();

        return ToDto(tx, null);
    }

    /// <summary>Dashboard financeiro (PRD §7/§12): previsto × recebido, despesas, resultado e contas a receber.</summary>
    public async Task<FinancialSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to)
    {
        var artistId = currentUser.EnsureArtist();

        // Contas vencidas em aberto viram OVERDUE (pagamento em atraso — PRD §23).
        var all = await db.FinancialTransactions
            .Where(t => t.ArtistId == artistId)
            .ToListAsync();
        var changed = false;
        foreach (var tx in all)
        {
            var before = tx.Status;
            tx.RefreshOverdueStatus(clock.Today);
            if (tx.Status != before) changed = true;
        }
        if (changed)
            await db.SaveChangesAsync();

        var period = all.Where(t => !t.DueDate.HasValue ||
                                    (!from.HasValue || t.DueDate >= from) &&
                                    (!to.HasValue || t.DueDate <= to));

        var incomes = period.Where(t => t.Type == TransactionType.Income && t.Status != TransactionStatus.Cancelled).ToList();
        var expenses = period.Where(t => t.Type == TransactionType.Expense && t.Status != TransactionStatus.Cancelled).ToList();

        var incomeExpected = incomes.Sum(t => t.Amount);
        var incomeReceived = incomes.Where(t => t.Status == TransactionStatus.Received).Sum(t => t.Amount);
        var expenseTotal = expenses.Sum(t => t.Amount);
        var expensePaid = expenses.Where(t => t.Status == TransactionStatus.Paid).Sum(t => t.Amount);

        var upcomingShows = await db.Events.AsNoTracking()
            .CountAsync(e => e.ArtistId == artistId &&
                             (e.Status == EventStatus.Confirmed || e.Status == EventStatus.Scheduled) &&
                             e.StartDateTime >= clock.UtcNow);

        return new FinancialSummaryDto(
            from?.ToDateTime(TimeOnly.MinValue), to?.ToDateTime(TimeOnly.MaxValue),
            incomeExpected, incomeReceived,
            incomes.Where(t => t.Status is TransactionStatus.Pending or TransactionStatus.Overdue).Sum(t => t.Amount),
            expenseTotal, expensePaid,
            expenses.Where(t => t.Status is TransactionStatus.Pending or TransactionStatus.Overdue).Sum(t => t.Amount),
            incomeReceived - expensePaid,
            incomes.Where(t => t.Status is TransactionStatus.Pending or TransactionStatus.Overdue).Sum(t => t.Amount),
            upcomingShows);
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0)
            throw new ValidationException("O valor deve ser maior que zero.");
    }

    private static TransactionDto ToDto(FinancialTransaction t, string? eventTitle) => new(
        t.Id, t.Type, t.Category, t.Amount, t.DueDate, t.SettlementDate, t.Status, t.EventId, eventTitle, t.Notes, t.CreatedAt);
}
