using ArtistPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Envia o PDF do ingresso por email.</summary>
[ApiController]
[Route("api/public/ticket-orders")]
[AllowAnonymous]
public class TicketEmailController(IEmailService emailService) : ControllerBase
{
    public record SendTicketEmailRequest(
        string Email,
        string BuyerName,
        string EventName,
        string EventDate,
        /// <summary>PDF do ingresso codificado em base64.</summary>
        string PdfBase64,
        string FileName
    );

    /// <summary>Envia o ingresso (PDF) por email para o comprador.</summary>
    [HttpPost("send-ticket-email")]
    public async Task<IActionResult> SendTicketEmail([FromBody] SendTicketEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "Email é obrigatório." });

        if (string.IsNullOrWhiteSpace(request.PdfBase64))
            return BadRequest(new { error = "PDF é obrigatório." });

        // Monta o HTML do email
        var html = $"""
        <h1>Seu ingresso está aqui! 🎫</h1>
        <p>Olá, <span class="highlight">{request.BuyerName}</span>!</p>
        <p>Segue em anexo o seu ingresso para o evento <strong>{request.EventName}</strong> no dia <strong>{request.EventDate}</strong>.</p>
        <p>Apresente o QR code na entrada do evento junto com um documento com foto.</p>
        <p style="color: #9ca3af; font-size: 13px; margin-top: 24px;">Este email foi enviado pela plataforma Reprissa a pedido do comprador.</p>
        """;

        // Renderiza no layout base
        var fullHtml = WrapInLayout(html, "Seu ingresso - Reprissa");

        var result = await emailService.SendAsync(new EmailMessage
        {
            To = request.Email,
            Subject = $"🎫 Seu ingresso: {request.EventName}",
            HtmlBody = fullHtml,
            Attachments =
            [
                new EmailAttachment
                {
                    FileName = request.FileName,
                    ContentBase64 = request.PdfBase64,
                    ContentType = "application/pdf",
                }
            ]
        });

        if (!result.Success)
            return StatusCode(500, new { error = result.Error });

        return Ok(new { message = "Ingresso enviado por email com sucesso!", messageId = result.MessageId });
    }

    private static string WrapInLayout(string content, string title) =>
        """
        <!DOCTYPE html>
        <html lang="pt-BR">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1.0" />
          <title>TITLE_PLACEHOLDER</title>
          <style>
            body { margin: 0; padding: 0; background-color: #f4f4f7; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; }
            .wrapper { max-width: 600px; margin: 0 auto; padding: 32px 16px; }
            .card { background: #ffffff; border-radius: 12px; padding: 40px 32px; box-shadow: 0 1px 3px rgba(0,0,0,0.08); }
            .logo { text-align: center; margin-bottom: 32px; }
            .logo span { font-size: 24px; font-weight: 800; background: linear-gradient(135deg, #8b5cf6, #7c3aed, #6d28d9); -webkit-background-clip: text; -webkit-text-fill-color: transparent; background-clip: text; letter-spacing: -0.5px; }
            h1 { color: #1a1a2e; font-size: 22px; font-weight: 700; margin: 0 0 16px; }
            p { color: #4a4a68; font-size: 15px; line-height: 1.7; margin: 0 0 16px; }
            .highlight { color: #7c3aed; font-weight: 600; }
            .footer { text-align: center; margin-top: 32px; padding-top: 24px; border-top: 1px solid #e8e8ed; }
            .footer p { color: #9ca3af; font-size: 12px; line-height: 1.6; }
            .footer a { color: #8b5cf6; text-decoration: none; }
          </style>
        </head>
        <body>
          <div class="wrapper">
            <div class="card">
              <div class="logo"><span>Reprissa</span></div>
              CONTENT_PLACEHOLDER
            </div>
            <div class="footer">
              <p>&copy; 2024 Reprissa. Todos os direitos reservados.</p>
              <p><a href="https://reprissa.com.br">reprissa.com.br</a> &middot; <a href="https://instagram.com/reprissaoficial">@reprissaoficial</a></p>
            </div>
          </div>
        </body>
        </html>
        """.Replace("TITLE_PLACEHOLDER", title).Replace("CONTENT_PLACEHOLDER", content);
}
