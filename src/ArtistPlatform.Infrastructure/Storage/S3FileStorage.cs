using Amazon.S3;
using Amazon.S3.Model;
using ArtistPlatform.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace ArtistPlatform.Infrastructure.Storage;

/// <summary>Armazenamento de arquivos em bucket S3-compatível (Railway Objects).</summary>
public class S3FileStorage : IFileStorage
{
    private const string KeyPrefix = "uploads";

    private readonly IAmazonS3 _client;
    private readonly S3Options _options;
    private readonly string _publicBaseUrl;

    public S3FileStorage(IOptions<S3Options> optionsSnapshot)
    {
        _options = optionsSnapshot.Value;

        var config = new AmazonS3Config
        {
            ServiceURL = _options.EndpointUrl,
            // Railway Objects (e outros S3-compatíveis) usam path-style: endpoint/bucket/key.
            ForcePathStyle = true,
            AuthenticationRegion = string.IsNullOrWhiteSpace(_options.Region) ? "us-east-1" : _options.Region
        };
        _client = new AmazonS3Client(_options.AccessKeyId, _options.SecretAccessKey, config);

        _publicBaseUrl = string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? $"{_options.EndpointUrl.TrimEnd('/')}/{_options.BucketName}"
            : _options.PublicBaseUrl.TrimEnd('/');
    }

    public async Task<string> SaveAsync(Guid ownerId, Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(ownerId, fileName);

        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType
        };

        // Opcional (S3__ObjectAcl = public-read): torna o objeto legível publicamente mesmo
        // em bucket privado — necessário para a URL salva no banco renderizar no navegador.
        if (!string.IsNullOrWhiteSpace(_options.ObjectAcl))
            request.CannedACL = new S3CannedACL(_options.ObjectAcl);

        await _client.PutObjectAsync(request, cancellationToken);

        return $"{_publicBaseUrl}/{key}";
    }

    public void Delete(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        var key = ExtractKey(url);
        if (string.IsNullOrWhiteSpace(key)) return;

        _client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key
        }).GetAwaiter().GetResult();
    }

    /// <summary>Chave única por dono (uploads/{ownerId}/arquivo) — preserva a extensão quando segura.</summary>
    private static string BuildKey(Guid ownerId, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (extension.Length > 10 || extension.Any(c => !char.IsLetterOrDigit(c) && c != '.'))
            extension = ".bin";

        return $"{KeyPrefix}/{ownerId}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
    }

    /// <summary>Remove a base pública (ou pega os dois últimos segmentos) para achar a chave do objeto.</summary>
    private string ExtractKey(string url)
    {
        var clean = url.Split('?', '#')[0].TrimEnd('/');
        if (clean.StartsWith(_publicBaseUrl, StringComparison.OrdinalIgnoreCase))
            clean = clean[_publicBaseUrl.Length..].TrimStart('/');

        var segments = clean.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 ? $"{segments[^2]}/{segments[^1]}" : segments.FirstOrDefault() ?? string.Empty;
    }
}
