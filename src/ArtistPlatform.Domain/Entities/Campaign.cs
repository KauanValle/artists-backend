using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

public enum CampaignType
{
    Date = 0,
    Goal = 1
}

/// <summary>
/// Campanha / landing page criada pelo admin.
/// Pode ser por data (início/fim) ou por meta (ex: 500 seguidores).
/// </summary>
public class Campaign : BaseEntity
{
    /// <summary>Slug da rota pública — ex: "sorteio-ingressos" → /sorteio-ingressos</summary>
    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Regras da campanha (texto livre, pode conter markdown).</summary>
    public string Rules { get; set; } = string.Empty;

    /// <summary>Tipo: por data ou por meta.</summary>
    public CampaignType Type { get; set; }

    /// <summary>Data de início (quando Type = Date).</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Data de fim (quando Type = Date).</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Descrição da meta (quando Type = Goal) — ex: "500 seguidores no Instagram".</summary>
    public string? GoalDescription { get; set; }

    /// <summary>Valor numérico da meta (quando Type = Goal).</summary>
    public long? GoalTarget { get; set; }

    /// <summary>Progresso atual da meta.</summary>
    public long? GoalCurrent { get; set; }

    /// <summary>Se a campanha está ativa e visível publicamente.</summary>
    public bool IsActive { get; set; } = true;
}
