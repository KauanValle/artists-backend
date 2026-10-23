using Amazon.S3;
using Amazon.S3.Model;
using ArtistPlatform.Application.Interfaces;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace ArtistPlatform.Infrastructure.Storage;

/// <summary>Armazenamento de arquivos em bucket S3-compatível (Railway Objects).</summary>
public class S3FileStorage : IFileStorage
{
    private const string KeyPrefix = "uploads";

    private static readonly Regex SafeNamePattern = new(@"^[0-9a-f]{32}\.[a-z0-9]{1,9}$", RegexOptions.Compiled);

    private readonly IAmazonS3 _client;
    private readonly S3Options _options;

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
    }

    public async Task<string> SaveAsync(Guid ownerId, Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var safeName = BuildSafeName(fileName);
        var key = BuildKey(ownerId, safeName);

        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType
        };

        // Opcional (S3__ObjectAcl = public-read): objeto legível publicamente em bucket público.
        // Com bucket privado, a leitura acontece pelo endpoint /api/files (backend tem as credenciais).
        if (!string.IsNullOrWhiteSpace(_options.ObjectAcl))
            request.CannedACL = new S3CannedACL(_options.ObjectAcl);

        await _client.PutObjectAsync(request, cancellationToken);

        // URL servida pelo próprio backend (rota /api/files) — funciona com bucket privado.
        return $"/api/files/{ownerId}/{safeName}";
    }

    public async Task<(Stream Content, string ContentType)?> ReadAsync(Guid ownerId, string fileName)
    {
        if (!IsValidFileName(fileName))
            return null;

        try
        {
            var response = await _client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _options.BucketName,
                Key = BuildKey(ownerId, fileName)
            });
            return (response.ResponseStream, response.Headers.ContentType ?? "application/octet-stream");
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound
                                            || ex.ErrorCode == "NoSuchKey"
                                            || ex.ErrorCode == "AccessDenied")
        {
            return null;
        }
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

    /// <summary>Nome seguro: guid + extensão original quando curta e alfanumérica.</summary>
    private static string BuildSafeName(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (extension.Length > 10 || extension.Any(c => !char.IsLetterOrDigit(c) && c != '.'))
            extension = ".bin";

        return $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
    }

    /// <summary>Só aceita o formato que o próprio storage gera ({guid32 hex}.{ext}) — barra fora do caminho.</summary>
    public static bool IsValidFileName(string fileName) =>
        SafeNamePattern.IsMatch(fileName);

    private static string BuildKey(Guid ownerId, string safeName) => $"{KeyPrefix}/{ownerId}/{safeName}";

    /// <summary>Aceita URL servida pelo backend (/api/files/{ownerId}/{arquivo}) ou a chave direta no bucket.</summary>
    private string ExtractKey(string url)
    {
        var clean = url.Split('?', '#')[0].TrimEnd('/');
        var marker = $"/{KeyPrefix}/";
        var index = clean.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? string.Empty : clean[(index + marker.Length)..];
    }
}
