using ArtistPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

/// <summary>
/// Serve arquivos gravados no storage (ex.: fotos de perfil) sem exigir bucket público:
/// o backend lê o objeto com as próprias credenciais e entrega o stream.
/// URL devolvida no upload: /api/files/{ownerId}/{arquivo} (guid, sem risco de path traversal).
/// </summary>
[ApiController]
[Route("api/files")]
public class FilesController(IFileStorage fileStorage) : ControllerBase
{
    [HttpGet("{ownerId:guid}/{fileName}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(Guid ownerId, string fileName)
    {
        var file = await fileStorage.ReadAsync(ownerId, fileName);
        if (file is null)
            return NotFound();

        // Nome do arquivo é um guid novo a cada upload — conteúdo imutável, cache longo é seguro.
        Response.Headers.CacheControl = "public,max-age=86400";
        return File(file.Value.Content, file.Value.ContentType);
    }
}
