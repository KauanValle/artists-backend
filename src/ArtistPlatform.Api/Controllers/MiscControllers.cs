using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController(IReviewService reviewService) : ControllerBase
{
    /// <summary>Contratante avalia após o evento (RN014/RN015/RN016).</summary>
    [HttpPost]
    [Authorize(Roles = "Contractor")]
    public async Task<ActionResult<ReviewDto>> Create(CreateReviewRequestDto request)
        => Ok(await reviewService.CreateAsync(request));

    /// <summary>Avaliações públicas do artista.</summary>
    [HttpGet("artist/{artistId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<List<ReviewDto>>> ListForArtist(Guid artistId)
        => Ok(await reviewService.ListForArtistAsync(artistId));
}

[ApiController]
[Route("api/favorites")]
[Authorize(Roles = "Contractor")]
public class FavoritesController(IFavoriteService favoriteService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FavoriteDto>>> List()
        => Ok(await favoriteService.ListAsync());

    [HttpPost("{artistId:guid}")]
    public async Task<IActionResult> Add(Guid artistId)
    {
        await favoriteService.AddAsync(artistId);
        return NoContent();
    }

    [HttpDelete("{artistId:guid}")]
    public async Task<IActionResult> Remove(Guid artistId)
    {
        await favoriteService.RemoveAsync(artistId);
        return NoContent();
    }
}

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<NotificationDto>>> List([FromQuery] bool? unreadOnly)
        => Ok(await notifications.ListForUserAsync(unreadOnly));

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> UnreadCount()
        => Ok(await notifications.UnreadCountAsync());

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        await notifications.MarkReadAsync(id);
        return NoContent();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await notifications.MarkAllReadAsync();
        return NoContent();
    }
}

[ApiController]
[Route("api/contractors")]
[Authorize(Roles = "Contractor")]
public class ContractorsController(IContractorService contractorService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<ContractorDto>> GetMy()
        => Ok(await contractorService.GetMyAsync());

    [HttpPut("me")]
    public async Task<ActionResult<ContractorDto>> UpdateMy(SaveContractorRequestDto request)
        => Ok(await contractorService.UpdateMyAsync(request));
}
