using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Domain.Entities;

/// <summary>Membro de equipe sem conta própria (RN003) com divisão padrão (RN013).</summary>
public class TeamMember : BaseEntity
{
    public Guid ArtistId { get; set; }
    public string Name { get; set; } = default!;
    public string PhotoUrl { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public ShareType DefaultShareType { get; set; } = ShareType.Percentage;
    public decimal DefaultShareValue { get; set; }
    public string Notes { get; set; } = string.Empty;

    /// <summary>Valor a receber pelo membro sobre o total distribuível.</summary>
    public decimal CalculateShare(decimal distributable, ShareType? overrideType = null, decimal? overrideValue = null)
    {
        var type = overrideType ?? DefaultShareType;
        var value = overrideValue ?? DefaultShareValue;
        return type == ShareType.Percentage
            ? decimal.Round(distributable * value / 100m, 2)
            : value;
    }
}
