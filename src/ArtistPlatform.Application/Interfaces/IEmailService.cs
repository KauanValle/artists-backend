namespace ArtistPlatform.Application.Interfaces;

/// <summary>
/// Abstração para envio de emails. Totalmente desacoplada do provedor (Resend, SES, SMTP, etc.).
/// Para migrar de provedor, basta criar uma nova implementação desta interface.
/// </summary>
public interface IEmailService
{
    /// <summary>Envia um email simples com corpo HTML.</summary>
    Task<EmailResult> SendAsync(EmailMessage message, CancellationToken ct = default);

    /// <summary>Envia um email usando um template registrado.</summary>
    Task<EmailResult> SendTemplateAsync(string templateName, EmailTemplateData data, CancellationToken ct = default);
}

/// <summary>Mensagem de email genérica.</summary>
public record EmailMessage
{
    public required string To { get; init; }
    public required string Subject { get; init; }
    public required string HtmlBody { get; init; }
    public string? From { get; init; }
    public string? ReplyTo { get; init; }
    public List<string>? Cc { get; init; }
    public List<string>? Bcc { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public List<EmailAttachment>? Attachments { get; init; }
}

/// <summary>Anexo de email.</summary>
public record EmailAttachment
{
    /// <summary>Nome do arquivo (ex: ingresso.pdf).</summary>
    public required string FileName { get; init; }
    /// <summary>Conteúdo em base64.</summary>
    public required string ContentBase64 { get; init; }
    /// <summary>MIME type (ex: application/pdf).</summary>
    public string ContentType { get; init; } = "application/pdf";
}

/// <summary>Dados para renderizar um template de email.</summary>
public record EmailTemplateData
{
    public required string To { get; init; }
    public string? From { get; init; }
    public string? ReplyTo { get; init; }
    public Dictionary<string, string> Variables { get; init; } = new();
}

/// <summary>Resultado do envio.</summary>
public record EmailResult
{
    public bool Success { get; init; }
    public string? MessageId { get; init; }
    public string? Error { get; init; }

    public static EmailResult Ok(string? messageId = null) => new() { Success = true, MessageId = messageId };
    public static EmailResult Fail(string error) => new() { Success = false, Error = error };
}
