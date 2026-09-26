using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public interface IEquipmentService
{
    Task<List<EquipmentDto>> ListAsync(EquipmentStatus? status);
    Task<EquipmentDto> CreateAsync(SaveEquipmentRequestDto request);
    Task<EquipmentDto> UpdateAsync(Guid id, SaveEquipmentRequestDto request);
    Task DeleteAsync(Guid id);
}

public class EquipmentService(IAppDbContext db, ICurrentUserService currentUser) : IEquipmentService
{
    public Task<List<EquipmentDto>> ListAsync(EquipmentStatus? status)
    {
        var artistId = currentUser.EnsureArtist();
        var query = db.Equipment.AsNoTracking().Where(e => e.ArtistId == artistId);
        if (status.HasValue) query = query.Where(e => e.Status == status.Value);
        return query.OrderBy(e => e.Name)
            .Select(e => ToDto(e))
            .ToListAsync();
    }

    public async Task<EquipmentDto> CreateAsync(SaveEquipmentRequestDto request)
    {
        var artistId = currentUser.EnsureArtist();
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("O nome do equipamento é obrigatório.");

        var equipment = new Equipment
        {
            ArtistId = artistId,
            Name = request.Name.Trim(),
            Category = request.Category ?? string.Empty,
            Brand = request.Brand ?? string.Empty,
            Model = request.Model ?? string.Empty,
            Identifier = request.Identifier ?? string.Empty,
            WeightKg = request.WeightKg,
            Value = request.Value,
            Status = request.Status,
            Notes = request.Notes ?? string.Empty
        };
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();
        return ToDto(equipment);
    }

    public async Task<EquipmentDto> UpdateAsync(Guid id, SaveEquipmentRequestDto request)
    {
        var artistId = currentUser.EnsureArtist();
        var equipment = await db.Equipment.FirstOrDefaultAsync(e => e.Id == id && e.ArtistId == artistId)
            ?? throw new NotFoundException("Equipamento não encontrado.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("O nome do equipamento é obrigatório.");

        equipment.Name = request.Name.Trim();
        equipment.Category = request.Category ?? string.Empty;
        equipment.Brand = request.Brand ?? string.Empty;
        equipment.Model = request.Model ?? string.Empty;
        equipment.Identifier = request.Identifier ?? string.Empty;
        equipment.WeightKg = request.WeightKg;
        equipment.Value = request.Value;
        equipment.Status = request.Status;
        equipment.Notes = request.Notes ?? string.Empty;
        equipment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToDto(equipment);
    }

    public async Task DeleteAsync(Guid id)
    {
        var artistId = currentUser.EnsureArtist();
        var equipment = await db.Equipment.FirstOrDefaultAsync(e => e.Id == id && e.ArtistId == artistId)
            ?? throw new NotFoundException("Equipamento não encontrado.");
        db.Equipment.Remove(equipment);
        await db.SaveChangesAsync();
    }

    private static EquipmentDto ToDto(Equipment e) => new(
        e.Id, e.Name, e.Category, e.Brand, e.Model, e.Identifier, e.WeightKg, e.Value, e.Status, e.Notes);
}
