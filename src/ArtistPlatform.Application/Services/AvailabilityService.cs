using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public interface IAvailabilityService
{
    Task<List<AvailabilityDto>> ListByArtistAsync(Guid artistId, DateOnly? from, DateOnly? to);
    Task<List<AvailabilityDto>> CreateAsync(Guid userId, SaveAvailabilityRequest request);
    Task<AvailabilityDto> UpdateAsync(Guid userId, Guid id, SaveAvailabilityRequest request);
    Task DeleteAsync(Guid userId, Guid id);
}

public class AvailabilityService(IAppDbContext db, ICurrentUserService currentUser) : IAvailabilityService
{
    public async Task<List<AvailabilityDto>> ListByArtistAsync(Guid artistId, DateOnly? from, DateOnly? to)
    {
        var query = db.ArtistAvailabilities.AsNoTracking().Where(a => a.ArtistId == artistId);
        if (from.HasValue) query = query.Where(a => a.Date >= from.Value);
        if (to.HasValue) query = query.Where(a => a.Date <= to.Value);
        return await query.OrderBy(a => a.Date).ThenBy(a => a.StartTime)
            .Select(a => new AvailabilityDto(a.Id, a.ArtistId, a.Date, a.StartTime, a.EndTime, a.IsAllDay, a.Status, a.Note))
            .ToListAsync();
    }

    public async Task<List<AvailabilityDto>> CreateAsync(Guid userId, SaveAvailabilityRequest request)
    {
        var artistId = currentUser.EnsureArtist();

        if (!request.IsAllDay && request.EndTime <= request.StartTime)
            throw new ValidationException("O horário final deve ser maior que o inicial.");

        var start = request.Date.ToDateTime(request.IsAllDay ? TimeOnly.MinValue : request.StartTime);
        var end = request.Date.ToDateTime(request.IsAllDay ? new TimeOnly(23, 59, 59) : request.EndTime);
        await EnsureNoAgendaConflictAsync(artistId, new Interval(start, end), excludeAvailabilityId: null);

        var row = new ArtistAvailability
        {
            ArtistId = artistId,
            Date = request.Date,
            StartTime = request.IsAllDay ? TimeOnly.MinValue : request.StartTime,
            EndTime = request.EndTime,
            IsAllDay = request.IsAllDay,
            Status = request.Status,
            Note = request.Note
        };
        db.ArtistAvailabilities.Add(row);
        await db.SaveChangesAsync();

        return await ListByArtistAsync(artistId, request.Date, request.Date);
    }

    public async Task<AvailabilityDto> UpdateAsync(Guid userId, Guid id, SaveAvailabilityRequest request)
    {
        var artistId = currentUser.EnsureArtist();
        var row = await db.ArtistAvailabilities.FirstOrDefaultAsync(a => a.Id == id && a.ArtistId == artistId)
            ?? throw new NotFoundException("Disponibilidade não encontrada.");

        if (!request.IsAllDay && request.EndTime <= request.StartTime)
            throw new ValidationException("O horário final deve ser maior que o inicial.");

        var start = request.Date.ToDateTime(request.IsAllDay ? TimeOnly.MinValue : request.StartTime);
        var end = request.Date.ToDateTime(request.IsAllDay ? new TimeOnly(23, 59, 59) : request.EndTime);
        await EnsureNoAgendaConflictAsync(artistId, new Interval(start, end), excludeAvailabilityId: id);

        row.Date = request.Date;
        row.StartTime = request.IsAllDay ? TimeOnly.MinValue : request.StartTime;
        row.EndTime = request.EndTime;
        row.IsAllDay = request.IsAllDay;
        row.Status = request.Status;
        row.Note = request.Note;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new AvailabilityDto(row.Id, row.ArtistId, row.Date, row.StartTime, row.EndTime, row.IsAllDay, row.Status, row.Note);
    }

    public async Task DeleteAsync(Guid userId, Guid id)
    {
        var artistId = currentUser.EnsureArtist();
        var row = await db.ArtistAvailabilities.FirstOrDefaultAsync(a => a.Id == id && a.ArtistId == artistId)
            ?? throw new NotFoundException("Disponibilidade não encontrada.");
        db.ArtistAvailabilities.Remove(row);
        await db.SaveChangesAsync();
    }

    /// <summary>RN009: sem conflito de agenda com eventos e com outros bloqueios do mesmo dia.</summary>
    private async Task EnsureNoAgendaConflictAsync(Guid artistId, Interval interval, Guid? excludeAvailabilityId)
    {
        var hasEventConflict = await db.Events.AsNoTracking()
            .AnyAsync(e => e.ArtistId == artistId &&
                           (e.Status == EventStatus.Scheduled || e.Status == EventStatus.Confirmed) &&
                           e.StartDateTime < interval.End && e.EndDateTime > interval.Start);
        if (hasEventConflict)
            throw new BusinessRuleException("RN009: já existe um evento na agenda nesse horário.");

        var requestedDay = DateOnly.FromDateTime(interval.Start);
        var rows = await db.ArtistAvailabilities.AsNoTracking()
            .Where(a => a.ArtistId == artistId &&
                        (excludeAvailabilityId == null || a.Id != excludeAvailabilityId) &&
                        a.Date == requestedDay)
            .ToListAsync();
        foreach (var row in rows)
        {
            var ri = new Interval(
                row.Date.ToDateTime(row.StartTime),
                row.Date.ToDateTime(row.IsAllDay ? new TimeOnly(23, 59, 59) : row.EndTime));
            if (ri.Overlaps(interval))
                throw new BusinessRuleException("RN009: esse horário conflita com outro intervalo de disponibilidade.");
        }
    }
}
