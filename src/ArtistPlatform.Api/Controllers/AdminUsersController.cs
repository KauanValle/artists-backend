using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Gestão de usuários pelo administrador — listagem, stats, desativação, exclusão e despublicação.</summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController(
    IAppDbContext db,
    UserManager<AppUser> userManager,
    ITaxaAdministrativaService taxaService) : ControllerBase
{
    // ───────────────────── Artistas ─────────────────────

    /// <summary>Lista todos os artistas para o painel admin (publicados, não publicados, desativados).</summary>
    [HttpGet("artists")]
    public async Task<ActionResult<List<AdminArtistListDto>>> ListArtists()
    {
        var artists = await db.Artists
            .Include(a => a.Category)
            .ToListAsync();

        var artistIds = artists.Select(a => a.Id).ToList();

        // Perfis (para saber se está publicado)
        var profiles = await db.ArtistProfiles
            .Where(p => artistIds.Contains(p.ArtistId))
            .ToListAsync();

        // Reviews para média
        var reviews = await db.Reviews
            .Where(r => artistIds.Contains(r.ArtistId))
            .GroupBy(r => r.ArtistId)
            .Select(g => new { ArtistId = g.Key, Avg = g.Average(r => r.OverallRating), Count = g.Count() })
            .ToListAsync();

        // Bookings count
        var bookingCounts = await db.BookingRequests
            .Where(b => b.ArtistId != null && artistIds.Contains(b.ArtistId.Value))
            .GroupBy(b => b.ArtistId!.Value)
            .Select(g => new { ArtistId = g.Key, Total = g.Count() })
            .ToListAsync();

        // Users (para deactivation status)
        var users = await userManager.Users
            .Where(u => u.ArtistId != null && artistIds.Contains(u.ArtistId.Value))
            .ToListAsync();

        var result = artists.Select(a =>
        {
            var profile = profiles.FirstOrDefault(p => p.ArtistId == a.Id);
            var review = reviews.FirstOrDefault(r => r.ArtistId == a.Id);
            var booking = bookingCounts.FirstOrDefault(b => b.ArtistId == a.Id);
            var user = users.FirstOrDefault(u => u.ArtistId == a.Id);

            return new AdminArtistListDto(
                ArtistId: a.Id,
                UserId: user?.Id ?? Guid.Empty,
                Name: a.Name,
                ArtisticName: a.ArtisticName,
                ProfilePhotoUrl: a.ProfilePhotoUrl,
                CategoryName: a.Category?.Name ?? "",
                City: a.City,
                State: a.State,
                AverageRating: review?.Avg,
                ReviewCount: review?.Count ?? 0,
                IsPublished: profile?.IsPublished ?? false,
                IsDeactivated: user?.IsDeactivated ?? false,
                TotalBookings: booking?.Total ?? 0);
        }).ToList();

        return Ok(result);
    }

    /// <summary>Estatísticas completas de um artista para o painel admin.</summary>
    [HttpGet("artists/{artistId:guid}/stats")]
    public async Task<ActionResult<AdminArtistStatsDto>> GetArtistStats(Guid artistId)
    {
        var artist = await db.Artists
            .Include(a => a.Category)
            .FirstOrDefaultAsync(a => a.Id == artistId);

        if (artist is null)
            return NotFound("Artista não encontrado.");

        var profile = await db.ArtistProfiles.FirstOrDefaultAsync(p => p.ArtistId == artistId);
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.ArtistId == artistId);

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfYear = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var completedOrConfirmedStatuses = new[]
        {
            BookingStatus.Confirmed,
            BookingStatus.Completed
        };

        var allBookings = await db.BookingRequests
            .Where(b => b.ArtistId == artistId)
            .ToListAsync();

        var totalShows = allBookings.Count;
        var monthShows = allBookings.Count(b => b.CreatedAt >= startOfMonth);
        var yearShows = allBookings.Count(b => b.CreatedAt >= startOfYear);

        var valorConfirmadosConcluidos = allBookings
            .Where(b => completedOrConfirmedStatuses.Contains(b.Status))
            .Sum(b => b.Budget ?? 0m);

        var taxaPct = await taxaService.GetPercentualAsync();
        var taxaAdministrativa = valorConfirmadosConcluidos * (taxaPct / 100m);

        // Avaliações
        var reviews = await db.Reviews
            .Where(r => r.ArtistId == artistId)
            .ToListAsync();
        var averageRating = reviews.Count > 0 ? reviews.Average(r => r.OverallRating) : (double?)null;

        return Ok(new AdminArtistStatsDto(
            ArtistId: artistId,
            Name: artist.Name,
            ArtisticName: artist.ArtisticName,
            ProfilePhotoUrl: artist.ProfilePhotoUrl,
            CategoryName: artist.Category?.Name ?? "",
            City: artist.City,
            State: artist.State,
            AverageRating: averageRating,
            ReviewCount: reviews.Count,
            IsPublished: profile?.IsPublished ?? false,
            IsDeactivated: user?.IsDeactivated ?? false,
            DeactivationMessage: user?.DeactivationMessage,
            TotalShows: totalShows,
            MonthShows: monthShows,
            YearShows: yearShows,
            ValorConfirmadosConcluidos: valorConfirmadosConcluidos,
            TaxaAdministrativa: taxaAdministrativa));
    }

    /// <summary>Despublicar perfil do artista (remove do marketplace).</summary>
    [HttpPost("artists/{artistId:guid}/unpublish")]
    public async Task<IActionResult> UnpublishArtist(Guid artistId)
    {
        var profile = await db.ArtistProfiles.FirstOrDefaultAsync(p => p.ArtistId == artistId);
        if (profile is null)
            return NotFound("Perfil do artista não encontrado.");

        profile.IsPublished = false;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "Perfil despublicado com sucesso." });
    }

    /// <summary>Republicar perfil do artista (volta ao marketplace).</summary>
    [HttpPost("artists/{artistId:guid}/publish")]
    public async Task<IActionResult> PublishArtist(Guid artistId)
    {
        var profile = await db.ArtistProfiles.FirstOrDefaultAsync(p => p.ArtistId == artistId);
        if (profile is null)
            return NotFound("Perfil do artista não encontrado.");

        profile.IsPublished = true;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "Perfil publicado com sucesso." });
    }

    // ───────────────────── Contratantes ─────────────────────

    /// <summary>Lista todos os contratantes com estatísticas básicas.</summary>
    [HttpGet("contractors")]
    public async Task<ActionResult<List<AdminContractorListDto>>> ListContractors()
    {
        var contractors = await db.Contractors.ToListAsync();
        var contractorIds = contractors.Select(c => c.Id).ToList();

        var bookings = await db.BookingRequests
            .Where(b => b.ContractorId != null && contractorIds.Contains(b.ContractorId.Value))
            .ToListAsync();

        var taxaPct = await taxaService.GetPercentualAsync();

        var result = contractors.Select(c =>
        {
            var cBookings = bookings.Where(b => b.ContractorId == c.Id).ToList();
            var confirmedStatuses = new[] { BookingStatus.Confirmed, BookingStatus.Completed };
            var confirmedBookings = cBookings.Where(b => confirmedStatuses.Contains(b.Status)).ToList();
            var totalSpent = confirmedBookings.Sum(b => b.Budget ?? 0m);

            var user = userManager.Users.FirstOrDefault(u => u.ContractorId == c.Id);

            return new AdminContractorListDto(
                ContractorId: c.Id,
                UserId: c.UserId,
                Name: c.Name,
                CompanyName: string.IsNullOrWhiteSpace(c.CompanyName) ? null : c.CompanyName,
                City: c.City,
                State: c.State,
                Phone: c.Phone,
                TotalBookings: cBookings.Count,
                ConfirmedBookings: confirmedBookings.Count,
                TotalSpent: totalSpent,
                IsDeactivated: user?.IsDeactivated ?? false);
        }).ToList();

        return Ok(result);
    }

    /// <summary>Estatísticas completas de um contratante para o painel admin.</summary>
    [HttpGet("contractors/{contractorId:guid}/stats")]
    public async Task<ActionResult<AdminContractorStatsDto>> GetContractorStats(Guid contractorId)
    {
        var contractor = await db.Contractors.FirstOrDefaultAsync(c => c.Id == contractorId);
        if (contractor is null)
            return NotFound("Contratante não encontrado.");

        var user = await userManager.Users.FirstOrDefaultAsync(u => u.ContractorId == contractorId);

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfYear = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var confirmedStatuses = new[] { BookingStatus.Confirmed, BookingStatus.Completed };

        var bookings = await db.BookingRequests
            .Where(b => b.ContractorId == contractorId && confirmedStatuses.Contains(b.Status))
            .ToListAsync();

        var totalContratados = bookings.Count;
        var monthContratados = bookings.Count(b => b.CreatedAt >= startOfMonth);
        var yearContratados = bookings.Count(b => b.CreatedAt >= startOfYear);

        var taxaPct = await taxaService.GetPercentualAsync();
        var totalValue = bookings.Sum(b => b.Budget ?? 0m);
        var taxasPagas = totalValue * (taxaPct / 100m);

        return Ok(new AdminContractorStatsDto(
            ContractorId: contractorId,
            Name: contractor.Name,
            CompanyName: string.IsNullOrWhiteSpace(contractor.CompanyName) ? null : contractor.CompanyName,
            Phone: contractor.Phone,
            City: contractor.City,
            State: contractor.State,
            IsDeactivated: user?.IsDeactivated ?? false,
            DeactivationMessage: user?.DeactivationMessage,
            TotalContratados: totalContratados,
            MonthContratados: monthContratados,
            YearContratados: yearContratados,
            TaxasPagas: taxasPagas));
    }

    // ───────────────────── Ações compartilhadas ─────────────────────

    /// <summary>Desativa a conta de um usuário (artista ou contratante) com mensagem personalizada.</summary>
    [HttpPost("{userId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateUser(Guid userId, [FromBody] DeactivateUserRequest request)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return NotFound("Usuário não encontrado.");

        user.IsDeactivated = true;
        user.DeactivationMessage = request.Message;
        await userManager.UpdateAsync(user);

        return Ok(new { message = "Conta desativada com sucesso." });
    }

    /// <summary>Reativa a conta de um usuário.</summary>
    [HttpPost("{userId:guid}/reactivate")]
    public async Task<IActionResult> ReactivateUser(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return NotFound("Usuário não encontrado.");

        user.IsDeactivated = false;
        user.DeactivationMessage = null;
        await userManager.UpdateAsync(user);

        return Ok(new { message = "Conta reativada com sucesso." });
    }

    /// <summary>Exclui um usuário e seus dados associados.</summary>
    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return NotFound("Usuário não encontrado.");

        // Se for artista, remover perfil e dados relacionados
        if (user.ArtistId is not null)
        {
            var artistId = user.ArtistId.Value;

            var profile = await db.ArtistProfiles.FirstOrDefaultAsync(p => p.ArtistId == artistId);
            if (profile is not null)
                db.ArtistProfiles.Remove(profile);

            var fees = await db.ArtistFees.Where(f => f.ArtistId == artistId).ToListAsync();
            db.ArtistFees.RemoveRange(fees);

            var availabilities = await db.ArtistAvailabilities.Where(a => a.ArtistId == artistId).ToListAsync();
            db.ArtistAvailabilities.RemoveRange(availabilities);

            var reviews = await db.Reviews.Where(r => r.ArtistId == artistId).ToListAsync();
            db.Reviews.RemoveRange(reviews);

            var artist = await db.Artists.FirstOrDefaultAsync(a => a.Id == artistId);
            if (artist is not null)
                db.Artists.Remove(artist);
        }

        // Se for contratante, remover dados
        if (user.ContractorId is not null)
        {
            var contractor = await db.Contractors.FirstOrDefaultAsync(c => c.Id == user.ContractorId.Value);
            if (contractor is not null)
                db.Contractors.Remove(contractor);
        }

        // Remover refresh tokens
        var tokens = await db.RefreshTokens.Where(t => t.UserId == userId).ToListAsync();
        db.RefreshTokens.RemoveRange(tokens);

        await db.SaveChangesAsync();

        // Remover o usuário Identity
        await userManager.DeleteAsync(user);

        return Ok(new { message = "Usuário excluído com sucesso." });
    }

    /// <summary>Busca o userId pelo artistId.</summary>
    [HttpGet("artists/{artistId:guid}/user-id")]
    public async Task<ActionResult<object>> GetUserIdByArtist(Guid artistId)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.ArtistId == artistId);
        if (user is null)
            return NotFound("Usuário não encontrado para este artista.");
        return Ok(new { userId = user.Id });
    }

    /// <summary>Busca o userId pelo contractorId.</summary>
    [HttpGet("contractors/{contractorId:guid}/user-id")]
    public async Task<ActionResult<object>> GetUserIdByContractor(Guid contractorId)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.ContractorId == contractorId);
        if (user is null)
            return NotFound("Usuário não encontrado para este contratante.");
        return Ok(new { userId = user.Id });
    }
}
