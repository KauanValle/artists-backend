using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Tests;

/// <summary>Redes sociais do artista (Instagram) — salvamento, normalização e listagem pública.</summary>
public class SocialLinkTests
{
    private static (AppDbContext Db, FakeCurrentUser User, SocialLinkService Service, Artist Artist) Seed()
    {
        var db = TestDb.Create();
        var artist = new Artist
        {
            UserId = Guid.NewGuid(),
            Name = "João",
            ArtisticName = "DJ Peruca",
            City = "SP",
            State = "SP",
            Phone = "119",
            CategoryId = Guid.NewGuid()
        };
        db.Artists.Add(artist);
        db.SaveChanges();

        var user = new FakeCurrentUser { Role = UserRole.Artist, UserId = artist.UserId, ArtistId = artist.Id };
        return (db, user, new SocialLinkService(db, user), artist);
    }

    [Fact]
    public async Task Save_ComArroba_NormalizaSemArrobaEGeraUrl()
    {
        var (db, _, service, artist) = Seed();

        var result = await service.SaveAsync(artist.UserId, new SaveSocialLinkRequest("Instagram", "@DJ.Peruca"));

        Assert.Equal("dj.peruca", result!.Handle);
        Assert.Equal("https://instagram.com/dj.peruca", result.Url);
        Assert.Equal(1, await db.ArtistSocialLinks.CountAsync());
    }

    [Fact]
    public async Task Save_ComUrlDoPerfil_ExtraioHandle()
    {
        var (_, _, service, artist) = Seed();

        var result = await service.SaveAsync(artist.UserId, new SaveSocialLinkRequest(null, "https://www.instagram.com/dj.peruca/?hl=pt"));

        Assert.Equal("dj.peruca", result!.Handle);
    }

    [Fact]
    public async Task Save_DuasVezes_AtualizaSemDuplicar()
    {
        var (db, _, service, artist) = Seed();
        await service.SaveAsync(artist.UserId, new SaveSocialLinkRequest(null, "@dj.peruca"));

        var result = await service.SaveAsync(artist.UserId, new SaveSocialLinkRequest(null, "@outro.dj"));

        Assert.Equal("outro.dj", result!.Handle);
        Assert.Equal(1, await db.ArtistSocialLinks.CountAsync());
    }

    [Fact]
    public async Task Save_HandleVazio_RemoveVinculo()
    {
        var (db, _, service, artist) = Seed();
        await service.SaveAsync(artist.UserId, new SaveSocialLinkRequest(null, "@dj.peruca"));

        var result = await service.SaveAsync(artist.UserId, new SaveSocialLinkRequest(null, "  "));

        Assert.Null(result);
        Assert.Equal(0, await db.ArtistSocialLinks.CountAsync());
    }

    [Theory]
    [InlineData("usuario com espaço")]
    [InlineData("@dj-peruca-traco")]
    [InlineData("mais_de_30_caracteres_aaaaaaaaaaaaaaa")]
    public async Task Save_HandleInvalido_Rejeita(string handle)
    {
        var (_, _, service, artist) = Seed();

        await Assert.ThrowsAsync<ValidationException>(
            () => service.SaveAsync(artist.UserId, new SaveSocialLinkRequest(null, handle)));
    }

    [Fact]
    public async Task Save_PlataformaNaoSuportada_Rejeita()
    {
        var (_, _, service, artist) = Seed();

        await Assert.ThrowsAsync<ValidationException>(
            () => service.SaveAsync(artist.UserId, new SaveSocialLinkRequest("TikTok", "@dj.peruca")));
    }

    [Fact]
    public async Task ListByArtist_RetornaSomenteDoArtista()
    {
        var (db, _, service, artist) = Seed();
        var other = new Artist
        {
            UserId = Guid.NewGuid(), Name = "Maria", ArtisticName = "MC Maria",
            City = "RJ", State = "RJ", Phone = "219", CategoryId = Guid.NewGuid()
        };
        db.Artists.Add(other);
        db.ArtistSocialLinks.Add(new ArtistSocialLink { ArtistId = artist.Id, Platform = "Instagram", Handle = "dj.peruca" });
        db.ArtistSocialLinks.Add(new ArtistSocialLink { ArtistId = other.Id, Platform = "Instagram", Handle = "mc.maria" });
        await db.SaveChangesAsync();

        var links = await service.ListByArtistAsync(artist.Id);

        var link = Assert.Single(links);
        Assert.Equal("dj.peruca", link.Handle);
        Assert.Equal("https://instagram.com/dj.peruca", link.Url);
    }
}
