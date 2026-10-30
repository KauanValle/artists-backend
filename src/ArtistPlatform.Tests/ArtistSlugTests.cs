using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Tests;

/// <summary>
/// O slug público acompanha o nome artístico: muda quando o nome muda,
/// fica estável quando o nome não muda e ganha sufixo quando já ocupado.
/// </summary>
public class ArtistSlugTests
{
    private static (AppDbContext Db, ArtistService Service, Artist Artist, Guid CategoryId) Seed(string artisticName, string slug)
    {
        var db = TestDb.Create();
        var category = new ArtistCategory { Name = "Cantor", Slug = "cantor", IsActive = true };
        db.ArtistCategories.Add(category);
        var artist = new Artist
        {
            UserId = Guid.NewGuid(),
            Name = "Kauan",
            ArtisticName = artisticName,
            City = "SP",
            State = "SP",
            Phone = "119",
            CategoryId = category.Id
        };
        db.Artists.Add(artist);
        db.ArtistProfiles.Add(new ArtistProfile { ArtistId = artist.Id, Slug = slug });
        db.SaveChanges();
        return (db, new ArtistService(db), artist, category.Id);
    }

    private static SaveArtistRequest Request(Guid categoryId, string artisticName) => new(
        ArtistType: "Solo",
        Name: "Kauan",
        ArtisticName: artisticName,
        CategoryId: categoryId,
        City: "Curitiba",
        State: "PR",
        Phone: "41999999999",
        ProfilePhotoUrl: null,
        Description: "bio",
        Styles: [],
        Specialties: [],
        EventTypes: null,
        GalleryUrls: [],
        VideoUrls: []);

    [Fact]
    public async Task Salvar_ComNovoNomeArtistico_RegeneraSlug()
    {
        var (db, service, artist, categoryId) = Seed("DJ Chupetinha", "dj-chupetinha-0e8ee6");

        var result = await service.SaveOnboardingAsync(artist.UserId, Request(categoryId, "Kauan Valle"));

        Assert.Equal("kauan-valle", result.Slug);
        var perfil = await db.ArtistProfiles.SingleAsync(p => p.ArtistId == artist.Id);
        Assert.Equal("kauan-valle", perfil.Slug);
    }

    [Fact]
    public async Task Salvar_SemMudarNome_MantemSlugEstavel()
    {
        var (db, service, artist, categoryId) = Seed("DJ Chupetinha", "dj-chupetinha-0e8ee6");

        var result = await service.SaveOnboardingAsync(artist.UserId, Request(categoryId, "DJ Chupetinha"));

        Assert.Equal("dj-chupetinha-0e8ee6", result.Slug);
    }

    [Fact]
    public async Task Salvar_ComNomeCujoSlugJaExiste_GeraSlugComSufixo()
    {
        var db = TestDb.Create();
        var category = new ArtistCategory { Name = "Cantor", Slug = "cantor", IsActive = true };
        db.ArtistCategories.Add(category);
        var outro = new Artist
        {
            UserId = Guid.NewGuid(), Name = "Outro", ArtisticName = "Kauan Valle",
            City = "SP", State = "SP", Phone = "119", CategoryId = category.Id
        };
        db.Artists.Add(outro);
        db.ArtistProfiles.Add(new ArtistProfile { ArtistId = outro.Id, Slug = "kauan-valle" });
        var artist = new Artist
        {
            UserId = Guid.NewGuid(), Name = "Kauan", ArtisticName = "DJ Chupetinha",
            City = "SP", State = "SP", Phone = "119", CategoryId = category.Id
        };
        db.Artists.Add(artist);
        db.ArtistProfiles.Add(new ArtistProfile { ArtistId = artist.Id, Slug = "dj-chupetinha-0e8ee6" });
        db.SaveChanges();

        var service = new ArtistService(db);
        var result = await service.SaveOnboardingAsync(artist.UserId, Request(category.Id, "Kauan Valle"));

        Assert.StartsWith("kauan-valle-", result.Slug);
        Assert.NotEqual("kauan-valle", result.Slug);
    }
}
