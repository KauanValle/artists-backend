using System.Text.Json;
using ArtistPlatform.Application.Common;

namespace ArtistPlatform.Api.Middleware;

/// <summary>Mapeia exceções de domínio/aplicação para respostas HTTP consistentes.</summary>
public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var (status, title) = ex switch
            {
                UnauthorizedException => (StatusCodes.Status401Unauthorized, "Não autenticado"),
                ForbiddenException => (StatusCodes.Status403Forbidden, "Acesso negado"),
                NotFoundException => (StatusCodes.Status404NotFound, "Não encontrado"),
                ValidationException => (StatusCodes.Status400BadRequest, "Dados inválidos"),
                BusinessRuleException => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada"),
                _ => (StatusCodes.Status500InternalServerError, "Erro interno")
            };

            if (status == StatusCodes.Status500InternalServerError)
                logger.LogError(ex, "Erro não tratado em {Path}", context.Request.Path);

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            var payload = JsonSerializer.Serialize(new
            {
                error = ex.Message,
                title,
                status
            });
            await context.Response.WriteAsync(payload);
        }
    }
}
