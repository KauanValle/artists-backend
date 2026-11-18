namespace ArtistPlatform.Infrastructure.Email;

/// <summary>
/// Definição dos templates de email. Cada template tem um nome único e um body HTML.
/// Variáveis usam a sintaxe {{variavel}}.
/// Para adicionar um novo template, basta criar um novo EmailTemplate estático aqui
/// e registrá-lo no EmailTemplateService.
/// </summary>
public record EmailTemplate(string Name, string Subject, string Body);

public static class EmailTemplates
{
    // ─── Nomes dos templates (use esses nomes ao chamar SendTemplateAsync) ───
    public const string WelcomeArtistName = "welcome-artist";
    public const string WelcomeContractorName = "welcome-contractor";
    public const string PasswordResetName = "password-reset";
    public const string EmailConfirmationName = "email-confirmation";
    public const string BookingNotificationName = "booking-notification";
    public const string GenericName = "generic";

    public static readonly EmailTemplate WelcomeArtist = new(
        WelcomeArtistName,
        "Bem-vindo à Reprissa! 🎵",
        """
        <h1>Bem-vindo à Reprissa, {{nome}}!</h1>
        <p>Sua conta de artista foi criada com sucesso. Agora você faz parte da maior plataforma de conexão entre artistas e contratantes do Brasil.</p>
        <p>Complete seu perfil para começar a receber propostas:</p>
        <a href="{{link}}" class="btn">Completar meu perfil</a>
        <p>Se tiver qualquer dúvida, estamos por aqui. Bora crescer juntos! 🚀</p>
        """
    );

    public static readonly EmailTemplate WelcomeContractor = new(
        WelcomeContractorName,
        "Bem-vindo à Reprissa!",
        """
        <h1>Bem-vindo à Reprissa, {{nome}}!</h1>
        <p>Sua conta foi criada com sucesso. Agora você pode encontrar e contratar os melhores artistas para seus eventos.</p>
        <p>Comece explorando nosso catálogo:</p>
        <a href="{{link}}" class="btn">Buscar artistas</a>
        <p>Qualquer dúvida, conte com a gente!</p>
        """
    );

    public static readonly EmailTemplate PasswordReset = new(
        PasswordResetName,
        "Redefinir sua senha - Reprissa",
        """
        <h1>Redefinir senha</h1>
        <p>Olá, {{nome}}!</p>
        <p>Recebemos uma solicitação para redefinir a senha da sua conta. Clique no botão abaixo para criar uma nova senha:</p>
        <a href="{{link}}" class="btn">Redefinir minha senha</a>
        <p>Este link expira em <span class="highlight">{{expiracao}}</span>.</p>
        <p style="color: #9ca3af; font-size: 13px;">Se você não solicitou essa alteração, pode ignorar este email com segurança.</p>
        """
    );

    public static readonly EmailTemplate EmailConfirmation = new(
        EmailConfirmationName,
        "Confirme seu email - Reprissa",
        """
        <h1>Confirme seu email</h1>
        <p>Olá, {{nome}}!</p>
        <p>Para ativar sua conta na Reprissa, confirme seu endereço de email clicando no botão abaixo:</p>
        <a href="{{link}}" class="btn">Confirmar email</a>
        <p style="color: #9ca3af; font-size: 13px;">Se você não criou uma conta na Reprissa, pode ignorar este email.</p>
        """
    );

    public static readonly EmailTemplate BookingNotification = new(
        BookingNotificationName,
        "Nova proposta de show - Reprissa",
        """
        <h1>Você recebeu uma nova proposta! 🎉</h1>
        <p>Olá, {{nome}}!</p>
        <p>O contratante <span class="highlight">{{contratante}}</span> enviou uma proposta para um show:</p>
        <p><strong>Evento:</strong> {{evento}}<br/><strong>Data:</strong> {{data}}<br/><strong>Local:</strong> {{local}}<br/><strong>Cachê:</strong> {{cache}}</p>
        <a href="{{link}}" class="btn">Ver proposta</a>
        <p>Responda o mais rápido possível para não perder a oportunidade!</p>
        """
    );

    public static readonly EmailTemplate Generic = new(
        GenericName,
        "{{assunto}}",
        """
        <h1>{{titulo}}</h1>
        {{corpo}}
        """
    );
}
