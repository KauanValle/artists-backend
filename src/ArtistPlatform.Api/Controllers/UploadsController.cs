using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Upload de arquivos (foto do perfil, galeria). S3+CloudFront em produção; disco local no MVP.</summary>
[ApiController]
[Route("api/uploads")]
[Authorize]
public class UploadsController(IFileStorage fileStorage) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<UploadResultDto>> Upload(IFormFile file)
    {
        if (file.Length == 0)
            return BadRequest(new { error = "Arquivo vazio." });

        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "video/mp4" };
        if (!allowed.Contains(file.ContentType))
            return BadRequest(new { error = "Formato não suportado. Use JPEG, PNG, WebP ou MP4." });

        await using var stream = file.OpenReadStream();
        var url = await fileStorage.SaveAsync(stream, file.FileName, file.ContentType);
        return Ok(new UploadResultDto(url));
    }
}
