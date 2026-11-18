namespace ArtistPlatform.Application.Interfaces;

/// <summary>
/// Serviço de templates de email. Renderiza HTML a partir de templates registrados.
/// Desacoplado do provedor — funciona com qualquer IEmailService.
/// </summary>
public interface IEmailTemplateService
{
    /// <summary>Renderiza um template com as variáveis fornecidas.</summary>
    string Render(string templateName, Dictionary<string, string> variables);

    /// <summary>Verifica se um template existe.</summary>
    bool Exists(string templateName);
}
