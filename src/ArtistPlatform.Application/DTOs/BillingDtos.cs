namespace ArtistPlatform.Application.DTOs;

// ─────────────────────────── Billing / Assinatura ───────────────────────────

/// <summary>Retorno da criação do checkout no Asaas — o link leva o cliente ao pagamento.</summary>
public record CheckoutResponseDto(string CheckoutSessionId, string Link, string Status);

public record BillingDto(Guid Id, string CheckoutSessionId, string Link, string Status, DateTime CreatedAt);

/// <summary>Resultado do processamento do webhook (sempre 200 para o Asaas não reenviar).</summary>
public record WebhookResultDto(bool Handled, string Message);
