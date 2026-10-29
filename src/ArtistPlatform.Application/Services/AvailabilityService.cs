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
    Task<BatchAvailabilityResultDto> CreateBatchAsync(Guid userId, SaveAvailabilityBatchRequest request);
    Task<AvailabilityDto> UpdateAsync(Guid userId, Guid id, SaveAvailabilityRequest request);
    Task DeleteAsync(Guid userId, Guid id);
    Task<List<BusySlotDto>> GetBusySlotsAsync(Guid artistId, DateOnly? from, DateOnly? to);
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

        // O dia específico sobrescreve o padrão (ex.: "fechar" um dia de trabalho).
        await OverrideOverlappingIntervalsAsync(artistId, request, excludeAvailabilityId: null);
        await EnsureNoAgendaConflictAsync(artistId, BuildInterval(request), excludeAvailabilityId: null);

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

    /// <summary>
    /// Criação em lote (dias de trabalho padrão e datas marcadas na agenda visual):
    /// datas que já possuem disponibilidade são ignoradas, e dias com show confirmado
    /// na agenda são pulados (RN009 sem quebrar o lote).
    /// </summary>
    public async Task<BatchAvailabilityResultDto> CreateBatchAsync(Guid userId, SaveAvailabilityBatchRequest request)
    {
        var artistId = currentUser.EnsureArtist();

        var dates = (request.Dates ?? []).Distinct().OrderBy(d => d).ToList();
        if (dates.Count == 0)
            throw new ValidationException("Selecione ao menos uma data.");
        if (!request.IsAllDay && request.EndTime <= request.StartTime)
            throw new ValidationException("O horário final deve ser maior que o inicial.");

        // Datas que já possuem disponibilidade registrada — não duplica.
        var existingDates = (await db.ArtistAvailabilities.AsNoTracking()
                .Where(a => a.ArtistId == artistId && dates.Contains(a.Date))
                .Select(a => a.Date)
                .ToListAsync())
            .ToHashSet();

        // Eventos ativos no intervalo do lote — checados por dia em memória.
        var rangeStart = dates.First().ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rangeEnd = dates.Last().ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var activeEvents = await db.Events.AsNoTracking()
            .Where(e => e.ArtistId == artistId &&
                        (e.Status == EventStatus.Scheduled || e.Status == EventStatus.Confirmed) &&
                        e.StartDateTime < rangeEnd && e.EndDateTime > rangeStart)
            .Select(e => new { e.StartDateTime, e.EndDateTime })
            .ToListAsync();

        int created = 0, skippedExisting = 0, skippedConflict = 0;

        foreach (var date in dates)
        {
            if (existingDates.Contains(date))
            {
                skippedExisting++;
                continue;
            }

            var interval = new Interval(
                date.ToDateTime(request.IsAllDay ? TimeOnly.MinValue : request.StartTime, DateTimeKind.Utc),
                date.ToDateTime(request.IsAllDay ? new TimeOnly(23, 59, 59) : request.EndTime, DateTimeKind.Utc));

            if (activeEvents.Any(ev => ev.StartDateTime < interval.End && ev.EndDateTime > interval.Start))
            {
                skippedConflict++;
                continue;
            }

            db.ArtistAvailabilities.Add(new ArtistAvailability
            {
                ArtistId = artistId,
                Date = date,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                IsAllDay = request.IsAllDay,
                Status = request.Status,
                Note = request.Note
            });
            created++;
        }

        await db.SaveChangesAsync();
        return new BatchAvailabilityResultDto(created, skippedExisting, skippedConflict);
    }

    /// <summary>
    /// Janelas ocupadas por eventos (agendados/confirmados) do artista, expandidas por dia.
    /// Eventos de múltiplos dias viram uma janela por dia — a contratação usa isso para
    /// bloquear horários no calendário público.
    /// </summary>
    public async Task<List<BusySlotDto>> GetBusySlotsAsync(Guid artistId, DateOnly? from, DateOnly? to)
    {
        // Tempo de deslocamento/montagem do artista expande cada janela ocupada.
        var travelMinutes = await db.Artists.AsNoTracking()
            .Where(a => a.Id == artistId)
            .Select(a => a.TravelTimeMinutes)
            .FirstOrDefaultAsync();
        var buffer = TimeSpan.FromMinutes(Math.Max(0, travelMinutes));

        var query = db.Events.AsNoTracking()
            .Where(e => e.ArtistId == artistId &&
                        (e.Status == EventStatus.Scheduled || e.Status == EventStatus.Confirmed));

        if (from.HasValue)
        {
            var fromDt = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(e => e.EndDateTime > fromDt);
        }
        if (to.HasValue)
        {
            var toDt = to.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(e => e.StartDateTime < toDt);
        }

        var events = await query.Select(e => new { e.StartDateTime, e.EndDateTime }).ToListAsync();

        var slots = new List<BusySlotDto>();
        foreach (var ev in events)
        {
            // Janela com o deslocamento aplicado: montagem/deslocamento antes, desmontagem depois.
            var busyStart = ev.StartDateTime - buffer;
            var busyEnd = ev.EndDateTime + buffer;

            var day = DateOnly.FromDateTime(busyStart);
            var lastDay = DateOnly.FromDateTime(busyEnd);
            while (day <= lastDay)
            {
                var dayStartUtc = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var dayEndUtc = day.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
                var startTime = busyStart < dayStartUtc ? TimeOnly.MinValue : TimeOnly.FromDateTime(busyStart);
                var endTime = busyEnd > dayEndUtc ? TimeOnly.MaxValue : TimeOnly.FromDateTime(busyEnd);
                slots.Add(new BusySlotDto(day, startTime, endTime));
                day = day.AddDays(1);
            }
        }

        return slots.OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToList();
    }

    public async Task<AvailabilityDto> UpdateAsync(Guid userId, Guid id, SaveAvailabilityRequest request)
    {
        var artistId = currentUser.EnsureArtist();
        var row = await db.ArtistAvailabilities.FirstOrDefaultAsync(a => a.Id == id && a.ArtistId == artistId)
            ?? throw new NotFoundException("Disponibilidade não encontrada.");

        if (!request.IsAllDay && request.EndTime <= request.StartTime)
            throw new ValidationException("O horário final deve ser maior que o inicial.");

        await OverrideOverlappingIntervalsAsync(artistId, request, excludeAvailabilityId: id);
        await EnsureNoAgendaConflictAsync(artistId, BuildInterval(request), excludeAvailabilityId: id);

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

    private static TimeOnly EffectiveStart(SaveAvailabilityRequest request) =>
        request.IsAllDay ? TimeOnly.MinValue : request.StartTime;

    private static TimeOnly EffectiveEnd(SaveAvailabilityRequest request) =>
        request.IsAllDay ? new TimeOnly(23, 59, 59) : request.EndTime;

    private static Interval BuildInterval(SaveAvailabilityRequest request) =>
        new(request.Date.ToDateTime(EffectiveStart(request), DateTimeKind.Utc),
            request.Date.ToDateTime(EffectiveEnd(request), DateTimeKind.Utc));

    /// <summary>
    /// Dia específico sobrescreve o padrão: marcar INDISPONÍVEL remove qualquer intervalo que
    /// conflite no dia (ex.: "fechar" um dia de trabalho); abrir/ajustar remove apenas bloqueios
    /// de indisponibilidade. Conflitos com eventos e com intervalos do mesmo status seguem a RN009.
    /// </summary>
    private async Task OverrideOverlappingIntervalsAsync(Guid artistId, SaveAvailabilityRequest request, Guid? excludeAvailabilityId)
    {
        var effStart = EffectiveStart(request);
        var effEnd = EffectiveEnd(request);

        var overlapping = await db.ArtistAvailabilities
            .Where(a => a.ArtistId == artistId &&
                        a.Date == request.Date &&
                        a.StartTime < effEnd && a.EndTime > effStart &&
                        (excludeAvailabilityId == null || a.Id != excludeAvailabilityId))
            .ToListAsync();
        if (overlapping.Count == 0) return;

        var toRemove = request.Status == AvailabilityStatus.Unavailable
            ? overlapping
            : overlapping.Where(a => a.Status == AvailabilityStatus.Unavailable).ToList();

        if (toRemove.Count > 0)
        {
            db.ArtistAvailabilities.RemoveRange(toRemove);
            await db.SaveChangesAsync();
        }
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
                row.Date.ToDateTime(row.StartTime, DateTimeKind.Utc),
                row.Date.ToDateTime(row.IsAllDay ? new TimeOnly(23, 59, 59) : row.EndTime, DateTimeKind.Utc));
            if (ri.Overlaps(interval))
                throw new BusinessRuleException("RN009: esse horário conflita com outro intervalo de disponibilidade.");
        }
    }
}
