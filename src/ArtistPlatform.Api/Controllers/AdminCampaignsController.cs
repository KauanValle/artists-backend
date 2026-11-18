using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/campaigns")]
[Authorize(Roles = "Admin")]
public class AdminCampaignsController(IAppDbContext db) : ControllerBase
{
    public record CampaignDto(
        Guid Id,
        string Slug,
        string Name,
        string Description,
        string Rules,
        CampaignType Type,
        DateTime? StartDate,
        DateTime? EndDate,
        string? GoalDescription,
        long? GoalTarget,
        long? GoalCurrent,
        bool IsActive,
        DateTime CreatedAt
    );

    public record CreateCampaignRequest(
        string Slug,
        string Name,
        string Description,
        string Rules,
        CampaignType Type,
        DateTime? StartDate,
        DateTime? EndDate,
        string? GoalDescription,
        long? GoalTarget
    );

    public record UpdateCampaignRequest(
        string? Slug,
        string? Name,
        string? Description,
        string? Rules,
        CampaignType? Type,
        DateTime? StartDate,
        DateTime? EndDate,
        string? GoalDescription,
        long? GoalTarget,
        long? GoalCurrent,
        bool? IsActive
    );

    [HttpGet]
    public async Task<ActionResult<List<CampaignDto>>> GetAll()
    {
        var campaigns = await db.Campaigns
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CampaignDto(
                c.Id, c.Slug, c.Name, c.Description, c.Rules,
                c.Type, c.StartDate, c.EndDate,
                c.GoalDescription, c.GoalTarget, c.GoalCurrent,
                c.IsActive, c.CreatedAt))
            .ToListAsync();

        return Ok(campaigns);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CampaignDto>> GetById(Guid id)
    {
        var c = await db.Campaigns.FindAsync(id);
        if (c is null) return NotFound();

        return Ok(new CampaignDto(
            c.Id, c.Slug, c.Name, c.Description, c.Rules,
            c.Type, c.StartDate, c.EndDate,
            c.GoalDescription, c.GoalTarget, c.GoalCurrent,
            c.IsActive, c.CreatedAt));
    }

    [HttpPost]
    public async Task<ActionResult<CampaignDto>> Create(CreateCampaignRequest req)
    {
        // Verifica slug único
        var exists = await db.Campaigns.AnyAsync(c => c.Slug == req.Slug);
        if (exists) return Conflict(new { message = "Já existe uma campanha com esse slug." });

        var campaign = new Campaign
        {
            Slug = req.Slug.Trim().ToLowerInvariant(),
            Name = req.Name,
            Description = req.Description,
            Rules = req.Rules,
            Type = req.Type,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            GoalDescription = req.GoalDescription,
            GoalTarget = req.GoalTarget,
            GoalCurrent = 0,
            IsActive = true,
        };

        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        return Created($"/api/admin/campaigns/{campaign.Id}",
            new CampaignDto(
                campaign.Id, campaign.Slug, campaign.Name, campaign.Description, campaign.Rules,
                campaign.Type, campaign.StartDate, campaign.EndDate,
                campaign.GoalDescription, campaign.GoalTarget, campaign.GoalCurrent,
                campaign.IsActive, campaign.CreatedAt));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CampaignDto>> Update(Guid id, UpdateCampaignRequest req)
    {
        var campaign = await db.Campaigns.FindAsync(id);
        if (campaign is null) return NotFound();

        if (req.Slug is not null)
        {
            var slug = req.Slug.Trim().ToLowerInvariant();
            var exists = await db.Campaigns.AnyAsync(c => c.Slug == slug && c.Id != id);
            if (exists) return Conflict(new { message = "Já existe uma campanha com esse slug." });
            campaign.Slug = slug;
        }

        if (req.Name is not null) campaign.Name = req.Name;
        if (req.Description is not null) campaign.Description = req.Description;
        if (req.Rules is not null) campaign.Rules = req.Rules;
        if (req.Type is not null) campaign.Type = req.Type.Value;
        if (req.StartDate is not null) campaign.StartDate = req.StartDate;
        if (req.EndDate is not null) campaign.EndDate = req.EndDate;
        if (req.GoalDescription is not null) campaign.GoalDescription = req.GoalDescription;
        if (req.GoalTarget is not null) campaign.GoalTarget = req.GoalTarget;
        if (req.GoalCurrent is not null) campaign.GoalCurrent = req.GoalCurrent;
        if (req.IsActive is not null) campaign.IsActive = req.IsActive.Value;

        campaign.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new CampaignDto(
            campaign.Id, campaign.Slug, campaign.Name, campaign.Description, campaign.Rules,
            campaign.Type, campaign.StartDate, campaign.EndDate,
            campaign.GoalDescription, campaign.GoalTarget, campaign.GoalCurrent,
            campaign.IsActive, campaign.CreatedAt));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var campaign = await db.Campaigns.FindAsync(id);
        if (campaign is null) return NotFound();

        db.Campaigns.Remove(campaign);
        await db.SaveChangesAsync();

        return NoContent();
    }
}

// Endpoint público para exibir a landing page da campanha pelo slug
[ApiController]
[Route("api/campaigns")]
public class PublicCampaignsController(IAppDbContext db) : ControllerBase
{
    public record PublicCampaignDto(
        string Slug,
        string Name,
        string Description,
        string Rules,
        CampaignType Type,
        DateTime? StartDate,
        DateTime? EndDate,
        string? GoalDescription,
        long? GoalTarget,
        long? GoalCurrent
    );

    [HttpGet("{slug}")]
    public async Task<ActionResult<PublicCampaignDto>> GetBySlug(string slug)
    {
        var c = await db.Campaigns
            .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive);

        if (c is null) return NotFound();

        return Ok(new PublicCampaignDto(
            c.Slug, c.Name, c.Description, c.Rules,
            c.Type, c.StartDate, c.EndDate,
            c.GoalDescription, c.GoalTarget, c.GoalCurrent));
    }
}
