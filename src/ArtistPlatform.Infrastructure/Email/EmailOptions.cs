namespace ArtistPlatform.Infrastructure.Email;

/// <summary>
/// Configurações de email. Lido de appsettings.json seção "Email".
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Chave de API do provedor (Resend).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Email padrão do remetente (ex: contato@reprissa.com.br).</summary>
    public string DefaultFrom { get; set; } = "Reprissa <contato@reprissa.com.br>";

    /// <summary>Email de reply-to padrão (opcional).</summary>
    public string? DefaultReplyTo { get; set; }
}
