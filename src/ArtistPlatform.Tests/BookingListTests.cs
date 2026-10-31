using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Data;

namespace ArtistPlatform.Tests;

/// <summary>Listagem de shows: busca por nome/local, ordenação e paginação.</summary>
public class BookingListTests
{
    private (AppDbContext Db, FakeCurrentUser User, BookingService Service) Seed()
    {
        var db = TestDb.Create();
        var artist = new Artist
        {
            UserId = Guid.NewGuid(), Name = "João", ArtisticName = "DJ Peruca",
            City = "SP", State = "SP", Phone = "119", CategoryId = Guid.NewGuid()
        };
        db.Artists.Add(artist);

        var nomes = new[] { "Marcio", "Ana", "Bruno" };
        var datas = new[] { new DateOnly(2026, 12, 20), new DateOnly(2026, 11, 5), new DateOnly(2027, 1, 10) };
        var valores = new decimal?[] { 300m, 900m, null };
        var locais = new[] { "Floriano Peixoto 187", "Arena Sul", "Floriano Essenfelder 150" };

        for (var i = 0; i < 3; i++)
        {
            var contractor = new Contractor
            {
                UserId = Guid.NewGuid(), Name = nomes[i], Type = ContractorType.Person, Phone = "119"
            };
            db.Contractors.Add(contractor);
            db.BookingRequests.Add(new BookingRequest
            {
                ArtistId = artist.Id,
                ContractorId = contractor.Id,
                Status = BookingStatus.Confirmed,
                EventDate = datas[i],
                StartTime = new TimeOnly(20, 0),
                EndTime = new TimeOnly(23, 0),
                Location = locais[i],
                EventType = EventType.Show,
                Budget = valores[i]
            });
        }
        db.SaveChanges();

        var user = new FakeCurrentUser { Role = UserRole.Artist, UserId = artist.UserId, ArtistId = artist.Id };
        return (db, user, new BookingService(db, user, new FakeUserDirectoryList(), new NotificationService(db, user)));
    }

    private class FakeUserDirectoryList : IUserDirectory
    {
        public Task<Dictionary<Guid, UserInfo>> GetUsersAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
            => Task.FromResult(userIds.Distinct().ToDictionary(id => id, id => new UserInfo(id, "Usuário", null)));
    }

    [Fact]
    public async Task OrdenarPorData_Ascendente_TrazMaisAntigoPrimeiro()
    {
        var (db, user, service) = Seed();

        var pagina = await service.ListAsync(BookingStatus.Confirmed, null, 1, 10, ordenarPor: "data", direcao: "asc");

        Assert.Equal(3, pagina.TotalCount);
        Assert.Equal(new DateOnly(2026, 11, 5), pagina.Items[0].EventDate);
        Assert.Equal(new DateOnly(2026, 12, 20), pagina.Items[1].EventDate);
        Assert.Equal(new DateOnly(2027, 1, 10), pagina.Items[2].EventDate);
    }

    [Fact]
    public async Task OrdenarPorValor_Descendente_TrazMaiorOrcamentoPrimeiro()
    {
        var (_, _, service) = Seed();

        var pagina = await service.ListAsync(BookingStatus.Confirmed, null, 1, 10, ordenarPor: "valor", direcao: "desc");

        Assert.Equal(900m, pagina.Items[0].Budget);
    }

    [Fact]
    public async Task Buscar_PorLocal_TrazaSomenteCorrespondentes()
    {
        var (_, _, service) = Seed();

        var pagina = await service.ListAsync(BookingStatus.Confirmed, null, 1, 10, busca: "Floriano");

        Assert.Equal(2, pagina.TotalCount);
    }

    [Fact]
    public async Task Buscar_PorNomeDoContratante_TrazCorrespondentes()
    {
        var (_, _, service) = Seed();

        var pagina = await service.ListAsync(BookingStatus.Confirmed, null, 1, 10, busca: "Ana");

        Assert.Equal(1, pagina.TotalCount);
        Assert.Equal("Ana", pagina.Items[0].ContractorName);
    }

    [Fact]
    public async Task Paginacao_SegundaPagina_TrazRestante()
    {
        var (_, _, service) = Seed();

        var pagina1 = await service.ListAsync(BookingStatus.Confirmed, null, 1, 2);
        var pagina2 = await service.ListAsync(BookingStatus.Confirmed, null, 2, 2);

        Assert.Equal(3, pagina1.TotalCount);
        Assert.Equal(2, pagina1.Items.Count);
        Assert.Single(pagina2.Items);
        // Página 1 não repete itens da página 2.
        Assert.DoesNotContain(pagina2.Items.Select(b => b.Id), id => pagina1.Items.Any(b => b.Id == id));
    }
}
