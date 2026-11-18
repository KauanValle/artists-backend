using ArtistPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Endpoint de teste para envio de email (somente admin).</summary>
[ApiController]
[Route("api/admin/email")]
[Authorize(Roles = "Admin")]
public class AdminEmailController(IEmailService emailService) : ControllerBase
{
    public record SendTestRequest(string To, string Subject, string Body);

    /// <summary>Envia um email de teste.</summary>
    [HttpPost("test")]
    public async Task<IActionResult> SendTest([FromBody] SendTestRequest request)
    {
        var result = await emailService.SendAsync(new EmailMessage
        {
            To = request.To,
            Subject = request.Subject,
            HtmlBody = $"<h1>{request.Subject}</h1><p>{request.Body}</p>",
        });

        return result.Success
            ? Ok(new { result.MessageId, Message = "Email enviado com sucesso!" })
            : BadRequest(new { result.Error });
    }

    public record SendTemplateTestRequest(string To, string TemplateName, Dictionary<string, string> Variables);

    /// <summary>Envia um email de teste usando template.</summary>
    [HttpPost("test-template")]
    public async Task<IActionResult> SendTemplateTest([FromBody] SendTemplateTestRequest request)
    {
        var result = await emailService.SendTemplateAsync(request.TemplateName, new EmailTemplateData
        {
            To = request.To,
            Variables = request.Variables,
        });

        return result.Success
            ? Ok(new { result.MessageId, Message = "Email enviado com sucesso!" })
            : BadRequest(new { result.Error });
    }
}
