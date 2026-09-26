using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public interface IEventService
{
    Task<List<EventDto>> ListAsync(DateOnly? from, DateOnly? to, EventType? type, EventStatus? status);
    Task<EventDetailDto> GetAsync(Guid id);
    Task<EventDto> CreateAsync(CreateEventRequestDto request);
    Task<EventDto> UpdateAsync(Guid id, UpdateEventRequestDto request);
    Task DeleteAsync(Guid id);
    Task<EventEquipmentDto> AddEquipmentAsync(Guid eventId, AddEventEquipmentRequestDto request);
    Task ToggleEquipmentAsync(Guid eventId, Guid eventEquipmentId);
    Task RemoveEquipmentAsync(Guid eventId, Guid eventEquipmentId);
    Task<EventTeamShareDto> SetTeamShareAsync(Guid eventId, SetEventTeamShareRequestDto request);
    Task RemoveTeamShareAsync(Guid eventId, Guid teamMemberId);
    Task<TeamDivisionDto> GetDivisionAsync(Guid eventId);
}

public class EventService(IAppDbContext db, ICurrentUserService currentUser, IDateTimeClock clock) : IEventService
{
    public async Task<List<EventDto>> ListAsync(DateOnly? from, DateOnly? to, EventType? type, EventStatus? status)
    {
        var artistId = currentUser.EnsureArtist();
        var query = db.Events.AsNoTracking().Where(e => e.ArtistId == artistId);

        if (from.HasValue)
        {
            var fromDt = from.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(e => e.EndDateTime >= fromDt);
        }
        if (to.HasValue)
        {
            var toDt = to.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(e => e.StartDateTime <= toDt);
        }
        if (type.HasValue) query = query.Where(e => e.Type == type.Value);
        if (status.HasValue) query = query.Where(e => e.Status == status.Value);

        return await query.OrderBy(e => e.StartDateTime)
            .Select(e => new EventDto(e.Id, e.ArtistId, e.ContractorId, e.BookingRequestId, e.Title,
                e.Type, e.StartDateTime, e.EndDateTime, e.Location, e.Description, e.Status))
            .ToListAsync();
    }

    public async Task<EventDetailDto> GetAsync(Guid id)
    {
        var artistId = currentUser.EnsureArtist();
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.ArtistId == artistId)
            ?? throw new NotFoundException("Evento não encontrado.");

        var equipment = await db.EventEquipment.AsNoTracking()
            .Where(x => x.EventId == id)
            .Join(db.Equipment, x => x.EquipmentId, eq => eq.Id,
                (x, eq) => new EventEquipmentDto(x.Id, x.EventId, x.EquipmentId, eq.Name, x.IsChecked, x.Notes))
            .ToListAsync();

        var shares = await db.EventTeamShares.AsNoTracking()
            .Where(s => s.EventId == id)
            .Join(db.TeamMembers, s => s.TeamMemberId, m => m.Id,
                (s, m) => new EventTeamShareDto(s.Id, s.TeamMemberId, m.Name, s.ShareType, s.ShareValue))
            .ToListAsync();

        var division = await GetDivisionInternalAsync(ev, shares);

        return new EventDetailDto(
            new EventDto(ev.Id, ev.ArtistId, ev.ContractorId, ev.BookingRequestId, ev.Title, ev.Type,
                ev.StartDateTime, ev.EndDateTime, ev.Location, ev.Description, ev.Status),
            equipment, shares, division);
    }

    public async Task<EventDto> CreateAsync(CreateEventRequestDto request)
    {
        var artistId = currentUser.EnsureArtist();
        Validate(request.Title, request.StartDateTime, request.EndDateTime);

        var interval = new Interval(request.StartDateTime, request.EndDateTime);
        await EnsureNoConflictAsync(artistId, interval, excludeEventId: null);

        var ev = new Event
        {
            ArtistId = artistId,
            Title = request.Title.Trim(),
            Type = request.Type,
            StartDateTime = request.StartDateTime,
            EndDateTime = request.EndDateTime,
            Location = request.Location ?? string.Empty,
            Description = request.Description ?? string.Empty,
            Status = EventStatus.Scheduled
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        return ToDto(ev);
    }

    public async Task<EventDto> UpdateAsync(Guid id, UpdateEventRequestDto request)
    {
        var artistId = currentUser.EnsureArtist();
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == id && e.ArtistId == artistId)
            ?? throw new NotFoundException("Evento não encontrado.");

        Validate(request.Title, request.StartDateTime, request.EndDateTime);
        await EnsureNoConflictAsync(artistId, new Interval(request.StartDateTime, request.EndDateTime), excludeEventId: id);

        ev.Title = request.Title.Trim();
        ev.Type = request.Type;
        ev.StartDateTime = request.StartDateTime;
        ev.EndDateTime = request.EndDateTime;
        ev.Location = request.Location ?? string.Empty;
        ev.Description = request.Description ?? string.Empty;
        ev.Status = request.Status;
        ev.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return ToDto(ev);
    }

    public async Task DeleteAsync(Guid id)
    {
        var artistId = currentUser.EnsureArtist();
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == id && e.ArtistId == artistId)
            ?? throw new NotFoundException("Evento não encontrado.");
        db.Events.Remove(ev);
        await db.SaveChangesAsync();
    }

    public async Task<EventEquipmentDto> AddEquipmentAsync(Guid eventId, AddEventEquipmentRequestDto request)
    {
        var artistId = currentUser.EnsureArtist();
        var ev = await EnsureEventAsync(artistId, eventId);
        var equipment = await db.Equipment.AsNoTracking()
            .FirstOrDefaultAsync(eq => eq.Id == request.EquipmentId && eq.ArtistId == artistId)
            ?? throw new NotFoundException("Equipamento não encontrado.");

        var alreadyAdded = await db.EventEquipment.AnyAsync(x => x.EventId == eventId && x.EquipmentId == request.EquipmentId);
        if (alreadyAdded)
            throw new BusinessRuleException("Equipamento já está no checklist deste evento.");

        var item = new EventEquipment
        {
            EventId = eventId,
            EquipmentId = request.EquipmentId,
            Notes = request.Notes ?? string.Empty
        };
        db.EventEquipment.Add(item);

        if (equipment.Status == EquipmentStatus.Available)
        {
            var tracked = await db.Equipment.FirstAsync(eq => eq.Id == equipment.Id);
            tracked.Status = EquipmentStatus.InUse;
        }

        await db.SaveChangesAsync();
        return new EventEquipmentDto(item.Id, eventId, item.EquipmentId, equipment.Name, item.IsChecked, item.Notes);
    }

    public async Task ToggleEquipmentAsync(Guid eventId, Guid eventEquipmentId)
    {
        var artistId = currentUser.EnsureArtist();
        await EnsureEventAsync(artistId, eventId);
        var item = await db.EventEquipment.FirstOrDefaultAsync(x => x.Id == eventEquipmentId && x.EventId == eventId)
            ?? throw new NotFoundException("Item do checklist não encontrado.");
        item.IsChecked = !item.IsChecked;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task RemoveEquipmentAsync(Guid eventId, Guid eventEquipmentId)
    {
        var artistId = currentUser.EnsureArtist();
        await EnsureEventAsync(artistId, eventId);
        var item = await db.EventEquipment.FirstOrDefaultAsync(x => x.Id == eventEquipmentId && x.EventId == eventId)
            ?? throw new NotFoundException("Item do checklist não encontrado.");
        db.EventEquipment.Remove(item);
        await db.SaveChangesAsync();
    }

    public async Task<EventTeamShareDto> SetTeamShareAsync(Guid eventId, SetEventTeamShareRequestDto request)
    {
        var artistId = currentUser.EnsureArtist();
        await EnsureEventAsync(artistId, eventId);
        var member = await db.TeamMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.TeamMemberId && m.ArtistId == artistId)
            ?? throw new NotFoundException("Membro de equipe não encontrado.");

        ValidateShare(request.ShareType, request.ShareValue);

        var share = await db.EventTeamShares.FirstOrDefaultAsync(s => s.EventId == eventId && s.TeamMemberId == request.TeamMemberId);
        if (share is null)
        {
            share = new EventTeamShare { EventId = eventId, TeamMemberId = request.TeamMemberId };
            db.EventTeamShares.Add(share);
        }
        share.ShareType = request.ShareType;
        share.ShareValue = request.ShareValue;
        share.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new EventTeamShareDto(share.Id, share.TeamMemberId, member.Name, share.ShareType, share.ShareValue);
    }

    public async Task RemoveTeamShareAsync(Guid eventId, Guid teamMemberId)
    {
        var artistId = currentUser.EnsureArtist();
        await EnsureEventAsync(artistId, eventId);
        var share = await db.EventTeamShares.FirstOrDefaultAsync(s => s.EventId == eventId && s.TeamMemberId == teamMemberId)
            ?? throw new NotFoundException("Divisão não encontrada para este evento.");
        db.EventTeamShares.Remove(share);
        await db.SaveChangesAsync();
    }

    /// <summary>Divisão de cachê: cachê do evento − despesas = distribuível (PRD §13, RN013).</summary>
    public Task<TeamDivisionDto> GetDivisionAsync(Guid eventId)
        => GetAsync(eventId).ContinueWith(t => t.Result.Division!);

    private async Task<TeamDivisionDto> GetDivisionInternalAsync(Event ev, List<EventTeamShareDto> shares)
    {
        var fee = await db.FinancialTransactions.AsNoTracking()
            .Where(t => t.EventId == ev.Id && t.Type == TransactionType.Income && t.Status != TransactionStatus.Cancelled)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        var expenses = await db.FinancialTransactions.AsNoTracking()
            .Where(t => t.EventId == ev.Id && t.Type == TransactionType.Expense && t.Status != TransactionStatus.Cancelled)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        var distributable = Math.Max(0, fee - expenses);

        var lines = new List<TeamDivisionLineDto>();
        if (shares.Count > 0)
        {
            var memberIds = shares.Select(s => s.TeamMemberId).ToList();
            var members = await db.TeamMembers.AsNoTracking()
                .Where(m => memberIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.Name);

            foreach (var share in shares)
            {
                var amount = share.ShareType == ShareType.Percentage
                    ? decimal.Round(distributable * share.ShareValue / 100m, 2)
                    : share.ShareValue;
                lines.Add(new TeamDivisionLineDto(share.TeamMemberId, members.GetValueOrDefault(share.TeamMemberId, "Membro"),
                    share.ShareType, share.ShareValue, amount));
            }
        }

        return new TeamDivisionDto(fee, expenses, distributable, lines);
    }

    private async Task<Event> EnsureEventAsync(Guid artistId, Guid eventId)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId && e.ArtistId == artistId)
            ?? throw new NotFoundException("Evento não encontrado.");
        return ev;
    }

    /// <summary>RN009: o novo intervalo não pode conflitar com eventos nem bloqueios existentes.</summary>
    private async Task EnsureNoConflictAsync(Guid artistId, Interval interval, Guid? excludeEventId)
    {
        var conflicts = await db.Events.AsNoTracking()
            .Where(e => e.ArtistId == artistId &&
                        (e.Status == EventStatus.Scheduled || e.Status == EventStatus.Confirmed) &&
                        (excludeEventId == null || e.Id != excludeEventId) &&
                        e.StartDateTime < interval.End && e.EndDateTime > interval.Start)
            .Select(e => e.Title)
            .FirstOrDefaultAsync();
        if (conflicts is not null)
            throw new BusinessRuleException($"RN009: conflito de agenda com o evento \"{conflicts}\".");

        var date = DateOnly.FromDateTime(interval.Start);
        var blockedRows = await db.ArtistAvailabilities.AsNoTracking()
            .Where(a => a.ArtistId == artistId && a.Status != AvailabilityStatus.Available && a.Date == date)
            .ToListAsync();
        foreach (var row in blockedRows)
        {
            var ri = new Interval(
                row.Date.ToDateTime(row.StartTime),
                row.Date.ToDateTime(row.IsAllDay ? new TimeOnly(23, 59, 59) : row.EndTime));
            if (ri.Overlaps(interval))
                throw new BusinessRuleException("RN009: o horário conflita com um bloqueio de disponibilidade.");
        }
    }

    private static void Validate(string title, DateTime start, DateTime end)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ValidationException("O título é obrigatório.");
        if (end <= start)
            throw new ValidationException("O horário final deve ser maior que o inicial.");
    }

    private static void ValidateShare(ShareType type, decimal value)
    {
        if (type == ShareType.Percentage && (value < 0 || value > 100))
            throw new ValidationException("O percentual deve estar entre 0 e 100.");
        if (type == ShareType.Fixed && value < 0)
            throw new ValidationException("O valor fixo não pode ser negativo.");
    }

    private static EventDto ToDto(Event ev) =>
        new(ev.Id, ev.ArtistId, ev.ContractorId, ev.BookingRequestId, ev.Title, ev.Type,
            ev.StartDateTime, ev.EndDateTime, ev.Location, ev.Description, ev.Status);
}
