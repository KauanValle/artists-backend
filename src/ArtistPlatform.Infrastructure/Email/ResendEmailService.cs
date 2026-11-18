using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ArtistPlatform.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArtistPlatform.Infrastructure.Email;

/// <summary>
/// Implementação do IEmailService usando a API do Resend.
/// Para migrar de provedor, crie uma nova classe implementando IEmailService
/// e troque o registro no DI (DependencyInjection.cs).
/// </summary>
public class ResendEmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly EmailOptions _options;
    private readonly IEmailTemplateService _templates;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        HttpClient http,
        IOptions<EmailOptions> options,
        IEmailTemplateService templates,
        ILogger<ResendEmailService> logger)
    {
        _http = http;
        _options = options.Value;
        _templates = templates;
        _logger = logger;

        _http.BaseAddress = new Uri("https://api.resend.com/");
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public async Task<EmailResult> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        try
        {
            var payload = new ResendPayload
            {
                From = message.From ?? _options.DefaultFrom,
                To = [message.To],
                Subject = message.Subject,
                Html = message.HtmlBody,
                ReplyTo = message.ReplyTo ?? _options.DefaultReplyTo,
            };

            if (message.Cc?.Count > 0) payload.Cc = message.Cc;
            if (message.Bcc?.Count > 0) payload.Bcc = message.Bcc;
            if (message.Attachments?.Count > 0)
                payload.Attachments = message.Attachments.Select(a => new ResendAttachment
                {
                    Filename = a.FileName,
                    Content = a.ContentBase64,
                    Type = a.ContentType,
                }).ToList();

            var response = await _http.PostAsJsonAsync("emails", payload, ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ResendResponse>(ct);
                _logger.LogInformation("Email enviado para {To} (id: {Id})", message.To, result?.Id);
                return EmailResult.Ok(result?.Id);
            }

            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Falha ao enviar email para {To}: {Status} {Error}",
                message.To, response.StatusCode, error);
            return EmailResult.Fail($"{response.StatusCode}: {error}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar email para {To}", message.To);
            return EmailResult.Fail(ex.Message);
        }
    }

    public async Task<EmailResult> SendTemplateAsync(
        string templateName, EmailTemplateData data, CancellationToken ct = default)
    {
        if (!_templates.Exists(templateName))
            return EmailResult.Fail($"Template '{templateName}' não encontrado.");

        var html = _templates.Render(templateName, data.Variables);

        // Pega o subject do template se existir na variável, senão usa o nome do template
        var subject = data.Variables.GetValueOrDefault("assunto")
                   ?? data.Variables.GetValueOrDefault("title")
                   ?? templateName;

        return await SendAsync(new EmailMessage
        {
            To = data.To,
            Subject = subject,
            HtmlBody = html,
            From = data.From,
            ReplyTo = data.ReplyTo,
        }, ct);
    }

    #region Resend DTOs

    private class ResendPayload
    {
        [JsonPropertyName("from")] public string From { get; set; } = default!;
        [JsonPropertyName("to")] public List<string> To { get; set; } = default!;
        [JsonPropertyName("subject")] public string Subject { get; set; } = default!;
        [JsonPropertyName("html")] public string Html { get; set; } = default!;
        [JsonPropertyName("reply_to")] public string? ReplyTo { get; set; }
        [JsonPropertyName("cc")] public List<string>? Cc { get; set; }
        [JsonPropertyName("bcc")] public List<string>? Bcc { get; set; }
        [JsonPropertyName("attachments")] public List<ResendAttachment>? Attachments { get; set; }
    }

    private class ResendAttachment
    {
        [JsonPropertyName("filename")] public string Filename { get; set; } = default!;
        [JsonPropertyName("content")] public string Content { get; set; } = default!;
        [JsonPropertyName("type")] public string? Type { get; set; }
    }

    private class ResendResponse
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
    }

    #endregion
}
