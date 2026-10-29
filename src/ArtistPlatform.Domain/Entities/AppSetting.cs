using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>Configuração chave/valor da plataforma (ex.: logo personalizada enviada pelo admin).</summary>
public class AppSetting : BaseEntity
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = string.Empty;
}
