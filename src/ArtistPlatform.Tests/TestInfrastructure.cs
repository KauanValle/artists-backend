using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Tests;

internal class FakeCurrentUser : ICurrentUserService
{
    public Guid? UserId { get; set; }
    public Guid? ArtistId { get; set; }
    public Guid? ContractorId { get; set; }
    public UserRole? Role { get; set; }

    public bool IsInRole(UserRole role) => Role == role;

    public Guid EnsureAuthenticated()
        => UserId ?? throw new ForbiddenException();

    public Guid EnsureArtist()
    {
        if (Role != UserRole.Artist || ArtistId is null)
            throw new ForbiddenException("Apenas artistas podem executar esta ação.");
        return ArtistId.Value;
    }

    public Guid EnsureContractor()
    {
        if (Role != UserRole.Contractor || ContractorId is null)
            throw new ForbiddenException("Apenas contratantes podem executar esta ação.");
        return ContractorId.Value;
    }
}

internal class FakeClock(DateTime utcNow) : IDateTimeClock
{
    public DateTime UtcNow => utcNow;
    public DateOnly Today => DateOnly.FromDateTime(utcNow.ToLocalTime().Date);
}

internal static class TestDb
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"artistplatform-test-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
