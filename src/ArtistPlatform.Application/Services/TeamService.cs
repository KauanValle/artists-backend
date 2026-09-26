using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public interface ITeamService
{
    Task<List<TeamMemberDto>> ListAsync();
    Task<TeamMemberDto> CreateAsync(SaveTeamMemberRequestDto request);
    Task<TeamMemberDto> UpdateAsync(Guid id, SaveTeamMemberRequestDto request);
    Task DeleteAsync(Guid id);
}

/// <summary>Equipe sem contas próprias (RN003) com divisão padrão de cachê (PRD §13).</summary>
public class TeamService(IAppDbContext db, ICurrentUserService currentUser) : ITeamService
{
    public Task<List<TeamMemberDto>> ListAsync()
    {
        var artistId = currentUser.EnsureArtist();
        return db.TeamMembers.AsNoTracking()
            .Where(m => m.ArtistId == artistId)
            .OrderBy(m => m.Name)
            .Select(m => ToDto(m))
            .ToListAsync();
    }

    public async Task<TeamMemberDto> CreateAsync(SaveTeamMemberRequestDto request)
    {
        var artistId = currentUser.EnsureArtist();
        Validate(request);

        var member = new TeamMember
        {
            ArtistId = artistId,
            Name = request.Name.Trim(),
            PhotoUrl = request.PhotoUrl ?? string.Empty,
            Role = request.Role ?? string.Empty,
            Phone = request.Phone ?? string.Empty,
            Email = request.Email ?? string.Empty,
            DefaultShareType = request.DefaultShareType,
            DefaultShareValue = request.DefaultShareValue,
            Notes = request.Notes ?? string.Empty
        };
        db.TeamMembers.Add(member);
        await db.SaveChangesAsync();
        return ToDto(member);
    }

    public async Task<TeamMemberDto> UpdateAsync(Guid id, SaveTeamMemberRequestDto request)
    {
        var artistId = currentUser.EnsureArtist();
        var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == id && m.ArtistId == artistId)
            ?? throw new NotFoundException("Membro não encontrado.");
        Validate(request);

        member.Name = request.Name.Trim();
        member.PhotoUrl = request.PhotoUrl ?? string.Empty;
        member.Role = request.Role ?? string.Empty;
        member.Phone = request.Phone ?? string.Empty;
        member.Email = request.Email ?? string.Empty;
        member.DefaultShareType = request.DefaultShareType;
        member.DefaultShareValue = request.DefaultShareValue;
        member.Notes = request.Notes ?? string.Empty;
        member.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToDto(member);
    }

    public async Task DeleteAsync(Guid id)
    {
        var artistId = currentUser.EnsureArtist();
        var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == id && m.ArtistId == artistId)
            ?? throw new NotFoundException("Membro não encontrado.");
        db.TeamMembers.Remove(member);
        await db.SaveChangesAsync();
    }

    private static void Validate(SaveTeamMemberRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("O nome do membro é obrigatório.");
        if (request.DefaultShareType == ShareType.Percentage && (request.DefaultShareValue < 0 || request.DefaultShareValue > 100))
            throw new ValidationException("O percentual deve estar entre 0 e 100.");
        if (request.DefaultShareValue < 0)
            throw new ValidationException("O valor da divisão não pode ser negativo.");
    }

    private static TeamMemberDto ToDto(TeamMember m) => new(
        m.Id, m.Name, m.PhotoUrl, m.Role, m.Phone, m.Email, m.DefaultShareType, m.DefaultShareValue, m.Notes);
}
