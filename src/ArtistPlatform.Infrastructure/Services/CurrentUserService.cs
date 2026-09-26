using System.Security.Claims;
using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace ArtistPlatform.Infrastructure.Services;

public class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public Guid? ArtistId => GetGuidClaim("artistId");
    public Guid? ContractorId => GetGuidClaim("contractorId");

    public UserRole? Role
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<UserRole>(value, ignoreCase: true, out var role) ? role : null;
        }
    }

    public bool IsInRole(UserRole role) => Role == role;

    public Guid EnsureAuthenticated()
        => UserId ?? throw new UnauthorizedException("Usuário não autenticado.");

    public Guid EnsureArtist()
    {
        EnsureAuthenticated();
        if (Role != UserRole.Artist || ArtistId is null)
            throw new ForbiddenException("Apenas artistas podem executar esta ação.");
        return ArtistId.Value;
    }

    public Guid EnsureContractor()
    {
        EnsureAuthenticated();
        if (Role != UserRole.Contractor || ContractorId is null)
            throw new ForbiddenException("Apenas contratantes podem executar esta ação.");
        return ContractorId.Value;
    }

    private Guid? GetGuidClaim(string claimType)
    {
        var value = Principal?.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}

public class DateTimeClock : IDateTimeClock
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
}
