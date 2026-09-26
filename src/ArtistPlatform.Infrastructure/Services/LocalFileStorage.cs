using ArtistPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace ArtistPlatform.Infrastructure.Services;

/// <summary>Armazenamento local em wwwroot/uploads. Em produção, trocar por S3 + CloudFront (PRD §30).</summary>
public class LocalFileStorage(IWebHostEnvironment environment) : IFileStorage
{
    private const string Folder = "uploads";

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var root = Path.Combine(environment.ContentRootPath, "wwwroot", Folder);
        Directory.CreateDirectory(root);

        var extension = Path.GetExtension(fileName);
        if (extension.Length > 10 || extension.Any(c => !char.IsLetterOrDigit(c) && c != '.'))
            extension = ".bin";

        var safeName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(root, safeName);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);

        return $"/{Folder}/{safeName}";
    }

    public void Delete(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        var name = Path.GetFileName(url);
        var fullPath = Path.Combine(environment.ContentRootPath, "wwwroot", Folder, name);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }
}
