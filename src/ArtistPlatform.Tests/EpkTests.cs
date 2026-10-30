using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Data;
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

        var result = await service.SaveAsync(artist.UserId, new SaveEpkRequest("Bio exclusiva do EPK."));

        Assert.Equal("Bio exclusiva do EPK.", result.Bio);
        Assert.Equal(1, await db.ArtistEpks.CountAsync());
    }

    [Fact]
    public async Task Save_DuasVezes_AtualizaSemDuplicar()
    {
        var (db, _, service, artist) = Seed();
        await service.SaveAsync(artist.UserId, new SaveEpkRequest("primeira versão"));

        var result = await service.SaveAsync(artist.UserId, new SaveEpkRequest("versão final"));

        Assert.Equal("versão final", result.Bio);
        Assert.Equal(1, await db.ArtistEpks.CountAsync());
    }

    [Fact]
    public async Task Save_Vazio_LimpaBio()
    {
        var (_, _, service, artist) = Seed();
        await service.SaveAsync(artist.UserId, new SaveEpkRequest("bio"));

        var result = await service.SaveAsync(artist.UserId, new SaveEpkRequest("   "));

        Assert.Equal(string.Empty, result.Bio);
    }

    [Fact]
    public async Task Save_MaiorQue4000Caracteres_Rejeita()
    {
        var (_, _, service, artist) = Seed();

        await Assert.ThrowsAsync<ValidationException>(
            () => service.SaveAsync(artist.UserId, new SaveEpkRequest(new string('a', 4001))));
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
    public async Task GetByArtist_ComEpk_RetornaBio()
    {
        var (db, _, service, artist) = Seed();
        db.ArtistEpks.Add(new ArtistEpk { ArtistId = artist.Id, Bio = "bio do kit" });
        await db.SaveChangesAsync();

        var result = await service.GetByArtistAsync(artist.Id);

        Assert.Equal("bio do kit", result.Bio);
    }
}
