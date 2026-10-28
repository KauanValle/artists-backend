using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Tests;

/// <summary>Geração e assinatura eletrônica do contrato de show (Jurídico).</summary>
public class ContractServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 15, 30, 45, DateTimeKind.Utc);

    private static (AppDbContext Db, FakeCurrentUser User, ContractService Service, Artist Artist, BookingRequest Booking) Arrange(
        BookingStatus status = BookingStatus.Confirmed)
    {
        var db = TestDb.Create();
        var user = new FakeCurrentUser { Role = UserRole.Artist, UserId = Guid.NewGuid(), ArtistId = Guid.NewGuid() };
        var artist = new Artist
        {
            Id = user.ArtistId!.Value,
            UserId = user.UserId!.Value,
            Name = "João Pereira",
            ArtisticName = "DJ Teste",
            City = "SP",
            State = "SP",
            Phone = "11988887777",
            CategoryId = Guid.NewGuid()
        };
        var booking = new BookingRequest
        {
            ArtistId = artist.Id,
            GuestName = "Kauan",
            GuestPhone = "41988887777",
            EventDate = new DateOnly(2026, 11, 6),
            StartTime = new TimeOnly(20, 0),
            EndTime = new TimeOnly(22, 0),
            Location = "Bar do Kauan",
            Budget = 1000m,
            Status = status
        };
        db.Artists.Add(artist);
        db.BookingRequests.Add(booking);
        db.SaveChanges();
        var service = new ContractService(db, user, new FakeClock(Now));
        return (db, user, service, artist, booking);
    }

    private static SignArtistContractRequest SignRequest() => new(
        "123.456.789-00",
        "Rua das Flores, 100 — Curitiba/PR",
        "(41) 99999-0000 · joao@teste.com",
        "12.345.678/0001-00",
        "Kauan Silva",
        "Rua do Bar, 50 — Curitiba/PR",
        "(41) 98888-7777",
        "50% na confirmação, 50% no dia do evento",
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUg==");

    [Fact]
    public async Task Get_SemAssinatura_RetornaRascunhoAutoPreenchido()
    {
        var (_, _, service, artist, booking) = Arrange();

        var dto = await service.GetForBookingAsync(booking.Id);

        Assert.False(dto.Signed);
        Assert.Contains("CLÁUSULA 1ª", dto.ContractText);
        Assert.Contains(artist.ArtisticName, dto.ContractText);
        Assert.Contains("Kauan", dto.ContractText);
        Assert.Contains("Bar do Kauan", dto.ContractText);
        Assert.Equal("Kauan", dto.ContractorName);
        Assert.Equal(booking.Budget, dto.CacheValue);
        Assert.Null(dto.SignatureData);
    }

    [Fact]
    public async Task Sign_OutroArtista_Lanca()
    {
        var (db, _, _, _, booking) = Arrange();

        var otherUser = new FakeCurrentUser { Role = UserRole.Artist, UserId = Guid.NewGuid(), ArtistId = Guid.NewGuid() };
        var otherService = new ContractService(db, otherUser, new FakeClock(Now));

        await Assert.ThrowsAsync<ForbiddenException>(() => otherService.SignAsync(booking.Id, SignRequest(), "ip", "ua"));
    }

    [Fact]
    public async Task Sign_PersisteTextoHashEvidencias()
    {
        var (db, _, service, artist, booking) = Arrange();

        var dto = await service.SignAsync(booking.Id, SignRequest(), "177.25.10.5", "test-agent/1.0");

        Assert.True(dto.Signed);
        Assert.Equal(64, dto.ContentHash.Length);
        Assert.Equal(Now, dto.SignedAtUtc);
        Assert.Equal("177.25.10.5", dto.SignedIp);
        Assert.Equal("test-agent/1.0", dto.SignedUserAgent);

        var row = await db.SignatureContracts.SingleAsync(c => c.BookingRequestId == booking.Id);
        Assert.Contains(artist.Name, row.ArtistSignedName);
        Assert.Contains("123.456.789-00", row.ContractText);
        Assert.Contains("Bar do Kauan", row.ContractText);
        Assert.StartsWith("data:image/png;base64,", row.ArtistSignatureData);
        Assert.Equal(row.ContentHash, dto.ContentHash);
    }

    [Fact]
    public async Task Sign_Reassina_SubstituiAnterior()
    {
        var (db, _, service, _, booking) = Arrange();
        await service.SignAsync(booking.Id, SignRequest(), "1.1.1.1", "old-agent");

        var dto = await service.SignAsync(booking.Id, SignRequest(), "2.2.2.2", "new-agent/2.0");

        Assert.Equal(1, await db.SignatureContracts.CountAsync(c => c.BookingRequestId == booking.Id));
        Assert.Equal("2.2.2.2", dto.SignedIp);
    }

    [Fact]
    public async Task Sign_ContratacaoNaoConfirmada_Lanca()
    {
        var (_, _, service, _, booking) = Arrange(BookingStatus.Requested);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SignAsync(booking.Id, SignRequest(), "ip", "ua"));

        Assert.Contains("confirmadas", ex.Message);
    }

    [Fact]
    public async Task Sign_SemAssinaturaDesenhada_Lanca()
    {
        var (_, _, service, _, booking) = Arrange();
        var request = SignRequest() with { SignatureData = "" };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.SignAsync(booking.Id, request, "ip", "ua"));

        Assert.Contains("Assinatura", ex.Message);
    }
}
