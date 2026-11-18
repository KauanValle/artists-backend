using ArtistPlatform.Application.Interfaces;

namespace ArtistPlatform.Infrastructure.Email;

/// <summary>
/// Renderiza templates de email usando substituição de placeholders {{variavel}}.
/// Templates são registrados em código — fácil de migrar para outro projeto.
/// </summary>
public class EmailTemplateService : IEmailTemplateService
{
    private static readonly Dictionary<string, EmailTemplate> Templates = new();

    static EmailTemplateService()
    {
        Register(EmailTemplates.WelcomeArtist);
        Register(EmailTemplates.WelcomeContractor);
        Register(EmailTemplates.PasswordReset);
        Register(EmailTemplates.EmailConfirmation);
        Register(EmailTemplates.BookingNotification);
        Register(EmailTemplates.Generic);
    }

    private static void Register(EmailTemplate template)
        => Templates[template.Name] = template;

    public string Render(string templateName, Dictionary<string, string> variables)
    {
        if (!Templates.TryGetValue(templateName, out var template))
            throw new ArgumentException($"Template '{templateName}' não encontrado.");

        var body = template.Body;
        foreach (var (key, value) in variables)
            body = body.Replace($"{{{{{key}}}}}", value);

        // Wrap in base layout
        return BaseLayout.Replace("{{CONTENT}}", body)
                         .Replace("{{TITLE}}", variables.GetValueOrDefault("title", "Reprissa"));
    }

    public bool Exists(string templateName) => Templates.ContainsKey(templateName);

    #region Base Layout

    private const string BaseLayout = """
    <!DOCTYPE html>
    <html lang="pt-BR">
    <head>
      <meta charset="utf-8" />
      <meta name="viewport" content="width=device-width, initial-scale=1.0" />
      <title>{{TITLE}}</title>
      <style>
        body { margin: 0; padding: 0; background-color: #f4f4f7; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; }
        .wrapper { max-width: 600px; margin: 0 auto; padding: 32px 16px; }
        .card { background: #ffffff; border-radius: 12px; padding: 40px 32px; box-shadow: 0 1px 3px rgba(0,0,0,0.08); }
        .logo { text-align: center; margin-bottom: 32px; }
        .logo span { font-size: 24px; font-weight: 800; background: linear-gradient(135deg, #8b5cf6, #7c3aed, #6d28d9); -webkit-background-clip: text; -webkit-text-fill-color: transparent; background-clip: text; letter-spacing: -0.5px; }
        h1 { color: #1a1a2e; font-size: 22px; font-weight: 700; margin: 0 0 16px; }
        p { color: #4a4a68; font-size: 15px; line-height: 1.7; margin: 0 0 16px; }
        .btn { display: inline-block; background: linear-gradient(135deg, #8b5cf6, #7c3aed); color: #ffffff !important; text-decoration: none; padding: 14px 32px; border-radius: 10px; font-weight: 600; font-size: 15px; margin: 8px 0 24px; }
        .footer { text-align: center; margin-top: 32px; padding-top: 24px; border-top: 1px solid #e8e8ed; }
        .footer p { color: #9ca3af; font-size: 12px; line-height: 1.6; }
        .footer a { color: #8b5cf6; text-decoration: none; }
        .highlight { color: #7c3aed; font-weight: 600; }
      </style>
    </head>
    <body>
      <div class="wrapper">
        <div class="card">
          <div class="logo"><span>Reprissa</span></div>
          {{CONTENT}}
        </div>
        <div class="footer">
          <p>© 2024 Reprissa. Todos os direitos reservados.</p>
          <p><a href="https://reprissa.com.br">reprissa.com.br</a> · <a href="https://instagram.com/reprissaoficial">@reprissaoficial</a></p>
        </div>
      </div>
    </body>
    </html>
    """;

    #endregion
}
