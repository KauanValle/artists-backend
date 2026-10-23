namespace ArtistPlatform.Infrastructure.Storage;

/// <summary>
/// Bucket S3-compatível (Railway Objects) para os arquivos da plataforma.
/// Credenciais vêm do ambiente/appsettings: S3__EndpointUrl, S3__Region, S3__BucketName,
/// S3__AccessKeyId, S3__SecretAccessKey e S3__PublicBaseUrl (opcional).
/// </summary>
public class S3Options
{
    public const string SectionName = "S3";

    public string EndpointUrl { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;

    /// <summary>
    /// Base pública das URLs dos arquivos (precisa permitir leitura pública).
    /// Vazio = {EndpointUrl}/{BucketName}.
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}
