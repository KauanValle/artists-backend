using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Tests;

/// <summary>EPK do artista — bio específica salvamento e leitura pública.</summary>
public class EpkTests
{
    private static (AppDbContext Db, FakeCurrentUser User, EpkService Service, Artist Artist) Seed()
    {
        var db = TestDb.Create();
        var artist = new Artist
        {
            UserId = Guid.NewGuid(),
            Name = "João",
            ArtisticName = "DJ Chupetinha",
            City = "SP",
            State = "SP",
            Phone = "119",
            CategoryId = Guid.NewGuid()
        };
        db.Artists.Add(artist);
        db.SaveChanges();

        var user = new FakeCurrentUser { Role = UserRole.Artist, UserId = artist.UserId, ArtistId = artist.Id };
        return (db, user, new EpkService(db, user), artist);
    }

    [Fact]
    public async Task Save_CriaEpkQuandoNaoExiste()
    {
        var (db, _, service, artist) = Seed();

        var result = await service.SaveAsync(artist.UserId, new SaveEpkRequest(
            "Bio exclusiva do EPK.",
            ["/api/files/owner1/a.jpg"],
            ["https://youtube.com/watch?v=abc"],
            ["Mó Paz", "Vida Louca"],
            "https://open.spotify.com/artist/xyz",
            "https://youtube.com/@dj",
            "https://soundcloud.com/dj",
            "https://open.spotify.com/playlist/pl1"));

        Assert.Equal("Bio exclusiva do EPK.", result.Bio);
        Assert.Equal("/api/files/owner1/a.jpg", Assert.Single(result.PhotoUrls));
        Assert.Equal("https://youtube.com/watch?v=abc", Assert.Single(result.VideoUrls));
        Assert.Equal(1, await db.ArtistEpks.CountAsync());
    }

    [Fact]
    public async Task Save_DuasVezes_AtualizaSemDuplicar()
    {
        var (db, _, service, artist) = Seed();
        await service.SaveAsync(artist.UserId, new SaveEpkRequest("primeira versão", null, null, null, null, null, null, null));

        var result = await service.SaveAsync(artist.UserId, new SaveEpkRequest("versão final", null, null, null, null, null, null, null));

        Assert.Equal("versão final", result.Bio);
        Assert.Equal(1, await db.ArtistEpks.CountAsync());
    }

    [Fact]
    public async Task Save_Vazio_LimpaBio()
    {
        var (_, _, service, artist) = Seed();
        await service.SaveAsync(artist.UserId, new SaveEpkRequest("bio", null, null, null, null, null, null, null));

        var result = await service.SaveAsync(artist.UserId, new SaveEpkRequest("   ", null, null, null, null, null, null, null));

        Assert.Equal(string.Empty, result.Bio);
    }

    [Fact]
    public async Task Save_MaiorQue4000Caracteres_Rejeita()
    {
        var (_, _, service, artist) = Seed();

        await Assert.ThrowsAsync<ValidationException>(
            () => service.SaveAsync(artist.UserId, new SaveEpkRequest(new string('a', 4001), null, null, null, null, null, null, null)));
    }

    [Fact]
    public async Task GetByArtist_SemEpk_RetornaBioVazia()
    {
        var (_, _, service, artist) = Seed();

        var result = await service.GetByArtistAsync(artist.Id);

        Assert.Equal(artist.Id, result.ArtistId);
        Assert.Equal(string.Empty, result.Bio);
    }

    [Fact]
    public async Task Save_Midia_ExcedeLimites_Rejeita()
    {
        var (_, _, service, artist) = Seed();

        var doze = Enumerable.Range(1, 12).Select(i => $"/api/files/owner/f{i}.jpg").ToList();
        var treze = Enumerable.Range(1, 13).Select(i => $"/api/files/owner/f{i}.jpg").ToList();

        await service.SaveAsync(artist.UserId, new SaveEpkRequest(null, doze, null, null, null, null, null, null));
        await Assert.ThrowsAsync<ValidationException>(
            () => service.SaveAsync(artist.UserId, new SaveEpkRequest(null, treze, null, null, null, null, null, null)));
        await Assert.ThrowsAsync<ValidationException>(
            () => service.SaveAsync(artist.UserId, new SaveEpkRequest(null, null,
                Enumerable.Range(1, 9).Select(i => $"https://youtu.be/v{i}").ToList(), null, null, null, null, null)));
    }

    [Fact]
    public async Task Save_Midia_UrlInvalida_RejeitaEDeduplica()
    {
        var (_, _, service, artist) = Seed();

        await Assert.ThrowsAsync<ValidationException>(
            () => service.SaveAsync(artist.UserId, new SaveEpkRequest(null, ["não é url"], null, null, null, null, null, null)));

        var result = await service.SaveAsync(artist.UserId, new SaveEpkRequest(null,
            ["/api/files/owner/a.jpg", "/api/files/owner/a.jpg", ""], null, null, null, null, null, null));

        Assert.Single(result.PhotoUrls);
    }

    [Fact]
    public async Task GetByArtist_SemMidia_RetornaListasVazias()
    {
        var (_, _, service, artist) = Seed();

        var result = await service.GetByArtistAsync(artist.Id);

        Assert.Empty(result.PhotoUrls);
        Assert.Empty(result.VideoUrls);
    }

    [Fact]
    public async Task Save_Repertorio_PlataformasEPlaylist()
    {
        var (_, _, service, artist) = Seed();

        var result = await service.SaveAsync(artist.UserId, new SaveEpkRequest(
            null, null, null,
            ["Mó Paz", " Mó Paz ", "Vida Louca", ""],
            "https://open.spotify.com/artist/xyz",
            "  ",
            null,
            "https://open.spotify.com/playlist/pl1"));

        Assert.Equal(["Mó Paz", "Vida Louca"], result.Songs);
        Assert.Equal("https://open.spotify.com/artist/xyz", result.SpotifyUrl);
        Assert.Null(result.YouTubeUrl);
        Assert.Null(result.SoundCloudUrl);
        Assert.Equal("https://open.spotify.com/playlist/pl1", result.SpotifyPlaylistUrl);
    }

    [Fact]
    public async Task Save_Repertorio_ExcedeLimites_Rejeita()
    {
        var (_, _, service, artist) = Seed();

        var dezesseis = Enumerable.Range(1, 16).Select(i => $"Música {i}").ToList();
        await Assert.ThrowsAsync<ValidationException>(
            () => service.SaveAsync(artist.UserId, new SaveEpkRequest(null, null, null, dezesseis, null, null, null, null)));
        await Assert.ThrowsAsync<ValidationException>(
            () => service.SaveAsync(artist.UserId, new SaveEpkRequest(null, null, null, null, null, null, null, "nao-e-url")));
    }

    [Fact]
    public async Task GetByArtist_SemRepertorio_RetornaVazio()
    {
        var (_, _, service, artist) = Seed();

        var result = await service.GetByArtistAsync(artist.Id);

        Assert.Empty(result.Songs);
        Assert.Null(result.SpotifyUrl);
        Assert.Null(result.SpotifyPlaylistUrl);
    }

    [Fact]
    public async Task GetByArtist_ComEpk_RetornaBio()
    {
        var (db, _, service, artist) = Seed();
        db.ArtistEpks.Add(new ArtistEpk { ArtistId = artist.Id, Bio = "bio do kit" });
        await db.SaveChangesAsync();

        var result = await service.GetByArtistAsync(artist.Id);

        Assert.Equal("bio do kit", result.Bio);
    }
}
