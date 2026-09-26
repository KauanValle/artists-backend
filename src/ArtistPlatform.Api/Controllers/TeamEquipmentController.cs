using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

[ApiController]
[Route("api/team")]
[Authorize(Roles = "Artist")]
public class TeamController(ITeamService teamService) : ControllerBase
{
    /// <summary>Equipe sem contas (RN003) com divisão padrão de cachê (PRD §13).</summary>
    [HttpGet]
    public async Task<ActionResult<List<TeamMemberDto>>> List()
        => Ok(await teamService.ListAsync());

    [HttpPost]
    public async Task<ActionResult<TeamMemberDto>> Create(SaveTeamMemberRequestDto request)
        => Ok(await teamService.CreateAsync(request));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TeamMemberDto>> Update(Guid id, SaveTeamMemberRequestDto request)
        => Ok(await teamService.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await teamService.DeleteAsync(id);
        return NoContent();
    }
}

[ApiController]
[Route("api/equipment")]
[Authorize(Roles = "Artist")]
public class EquipmentController(IEquipmentService equipmentService) : ControllerBase
{
    /// <summary>Equipamentos com status AVAILABLE/IN_USE/MAINTENANCE/UNAVAILABLE (PRD §14).</summary>
    [HttpGet]
    public async Task<ActionResult<List<EquipmentDto>>> List([FromQuery] EquipmentStatus? status)
        => Ok(await equipmentService.ListAsync(status));

    [HttpPost]
    public async Task<ActionResult<EquipmentDto>> Create(SaveEquipmentRequestDto request)
        => Ok(await equipmentService.CreateAsync(request));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EquipmentDto>> Update(Guid id, SaveEquipmentRequestDto request)
        => Ok(await equipmentService.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await equipmentService.DeleteAsync(id);
        return NoContent();
    }
}
