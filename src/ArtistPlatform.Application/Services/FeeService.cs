using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public interface IFeeService
{
    Task<List<FeeDto>> ListByArtistAsync(Guid artistId);
    Task<List<FeeDto>> ListMineAsync();
    Task<FeeDto> CreateAsync(SaveFeeRequest request);
    Task<FeeDto> UpdateAsync(Guid id, SaveFeeRequest request);
    Task DeleteAsync(Guid id);
}

public class FeeService(IAppDbContext db, ICurrentUserService currentUser) : IFeeService
{
    public async Task<List<FeeDto>> ListByArtistAsync(Guid artistId)
    {
        return await db.ArtistFees.AsNoTracking()
            .Where(f => f.ArtistId == artistId)
            .OrderBy(f => f.DurationMinutes)
            .Select(f => new FeeDto(f.Id, f.ArtistId, f.DurationMinutes, f.Price, f.Description))
            .ToListAsync();
    }

    public Task<List<FeeDto>> ListMineAsync() => ListByArtistAsync(currentUser.EnsureArtist());

    public async Task<FeeDto> CreateAsync(SaveFeeRequest request)
    {
        Validate(request);
        var artistId = currentUser.EnsureArtist();

        var fee = new ArtistFee
        {
            ArtistId = artistId,
            DurationMinutes = request.DurationMinutes,
            Price = request.Price,
            Description = request.Description
        };
        db.ArtistFees.Add(fee);
        await db.SaveChangesAsync();

        return new FeeDto(fee.Id, fee.ArtistId, fee.DurationMinutes, fee.Price, fee.Description);
    }

    public async Task<FeeDto> UpdateAsync(Guid id, SaveFeeRequest request)
    {
        Validate(request);
        var artistId = currentUser.EnsureArtist();
        var fee = await db.ArtistFees.FirstOrDefaultAsync(f => f.Id == id && f.ArtistId == artistId)
            ?? throw new NotFoundException("Cachê não encontrado.");

        fee.DurationMinutes = request.DurationMinutes;
        fee.Price = request.Price;
        fee.Description = request.Description;
        fee.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new FeeDto(fee.Id, fee.ArtistId, fee.DurationMinutes, fee.Price, fee.Description);
    }

    public async Task DeleteAsync(Guid id)
    {
        var artistId = currentUser.EnsureArtist();
        var fee = await db.ArtistFees.FirstOrDefaultAsync(f => f.Id == id && f.ArtistId == artistId)
            ?? throw new NotFoundException("Cachê não encontrado.");
        db.ArtistFees.Remove(fee);
        await db.SaveChangesAsync();
    }

    private static void Validate(SaveFeeRequest request)
    {
        if (request.DurationMinutes <= 0)
            throw new ValidationException("A duração deve ser maior que zero.");
        if (request.Price <= 0)
            throw new ValidationException("O preço deve ser maior que zero.");
    }
}
