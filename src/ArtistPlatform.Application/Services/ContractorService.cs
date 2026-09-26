using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public interface IContractorService
{
    Task<ContractorDto> GetMyAsync();
    Task<ContractorDto> UpdateMyAsync(SaveContractorRequestDto request);
}

public class ContractorService(IAppDbContext db, ICurrentUserService currentUser) : IContractorService
{
    public async Task<ContractorDto> GetMyAsync()
    {
        var contractor = await db.Contractors.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == currentUser.EnsureContractor())
            ?? throw new NotFoundException("Perfil de contratante não encontrado.");
        return ToDto(contractor);
    }

    public async Task<ContractorDto> UpdateMyAsync(SaveContractorRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("O nome é obrigatório.");

        var contractor = await db.Contractors
            .FirstOrDefaultAsync(c => c.Id == currentUser.EnsureContractor())
            ?? throw new NotFoundException("Perfil de contratante não encontrado.");

        contractor.Type = request.Type;
        contractor.Name = request.Name.Trim();
        contractor.CompanyName = request.CompanyName;
        contractor.Phone = request.Phone ?? string.Empty;
        contractor.City = request.City ?? string.Empty;
        contractor.State = request.State ?? string.Empty;
        contractor.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return ToDto(contractor);
    }

    private static ContractorDto ToDto(Domain.Entities.Contractor c) =>
        new(c.Id, c.Type, c.Name, c.CompanyName, c.Phone, c.City, c.State);
}
