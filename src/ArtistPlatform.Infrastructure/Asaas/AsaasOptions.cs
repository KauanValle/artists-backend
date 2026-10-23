namespace ArtistPlatform.Infrastructure.Asaas;

/// <summary>
/// Configuração da API de checkouts do Asaas — todos os valores vêm do ambiente
/// (Asaas__AccessToken, Asaas__SuccessUrl, ...; veja o .env da raiz e o docker-compose).
/// </summary>
public class AsaasOptions
{
    public const string SectionName = "Asaas";

    public string ApiBaseUrl { get; set; } = "https://api-sandbox.asaas.com/v3";
    public string AccessToken { get; set; } = string.Empty;
    public string UserAgent { get; set; } = "ArtistPlatform/1.0.0";

    /// <summary>URLs de callback enviadas no checkout — landing pages do frontend.</summary>
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public string ExpiredUrl { get; set; } = string.Empty;

    public string PlanName { get; set; } = "Plano Mensal";
    public string PlanDescription { get; set; } = "ACESSO TOTAL A PLATAFORMA";
    public decimal PlanValue { get; set; } = 49.90m;
    public int MinutesToExpire { get; set; } = 10;

    /// <summary>Se preenchido, o webhook só é aceito quando o header asaas-access-token for igual.</summary>
    public string? WebhookToken { get; set; }
}
