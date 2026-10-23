using ArtistPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using System.Text.RegularExpressions;

namespace ArtistPlatform.Infrastructure.Services;

/// <summary>Armazenamento local em wwwroot/uploads — desenvolvimento sem bucket (produção usa S3).</summary>
public class LocalFileStorage(IWebHostEnvironment environment) : IFileStorage
{
    private const string Folder = "uploads";

    private static readonly Regex SafeNamePattern = new(@"^[0-9a-f]{32}\.[a-z0-9]{1,9}$", RegexOptions.Compiled);

    public async Task<string> SaveAsync(Guid ownerId, Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var root = Path.Combine(environment.ContentRootPath, "wwwroot", Folder, ownerId.ToString());
        Directory.CreateDirectory(root);

        var extension = Path.GetExtension(fileName);
        if (extension.Length > 10 || extension.Any(c => !char.IsLetterOrDigit(c) && c != '.'))
            extension = ".bin";

        var safeName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(root, safeName);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);

        // Servido pelo middleware de arquivos estáticos (wwwroot) no desenvolvimento.
        return $"/{Folder}/{ownerId}/{safeName}";
    }

    public Task<(Stream Content, string ContentType)?> ReadAsync(Guid ownerId, string fileName)
    {
        if (!SafeNamePattern.IsMatch(fileName))
            return Task.FromResult<(Stream, string)?>(null);

        var fullPath = Path.Combine(environment.ContentRootPath, "wwwroot", Folder, ownerId.ToString(), fileName);
        if (!File.Exists(fullPath))
            return Task.FromResult<(Stream, string)?>(null);

        var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
        if (!contentTypeProvider.TryGetContentType(fileName, out var contentType))
            contentType = "application/octet-stream";

        return Task.FromResult<(Stream, string)?>((File.OpenRead(fullPath), contentType));
    }

    public void Delete(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        // URL no formato /uploads/{ownerId}/{arquivo} — mantém os dois últimos segmentos.
        var segments = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2) return;

        var relativePath = Path.Combine(segments[^2], segments[^1]);
        var fullPath = Path.Combine(environment.ContentRootPath, "wwwroot", Folder, relativePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }
}
