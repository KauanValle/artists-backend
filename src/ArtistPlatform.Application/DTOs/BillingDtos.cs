namespace ArtistPlatform.Application.DTOs;

// ─────────────────────────── Billing / Assinatura ───────────────────────────

/// <summary>Retorno da criação do checkout no Asaas — o link leva o cliente ao pagamento.</summary>
public record CheckoutResponseDto(string CheckoutSessionId, string Link, string Status);

public record BillingDto(Guid Id, string CheckoutSessionId, string Link, string Status, DateTime CreatedAt);

/// <summary>Resultado do processamento do webhook (sempre 200 para o Asaas não reenviar).</summary>
public record WebhookResultDto(bool Handled, string Message);

// ─────────────────────────── Pagamento do show (InfinityPay) ───────────────────────────

/// <summary>URL do checkout do InfinityPay para a contratação.</summary>
public record CheckoutUrlDto(string CheckoutUrl);

/// <summary>Dados de criação do link de pagamento do show.</summary>
public record InfinityPayLinkRequest(decimal Amount, string Description, Guid PaymentRef);

public record InfinityPayLinkResult(string TransactionId, string CheckoutUrl);

// ─────────────────────────── Taxa administrativa da plataforma ───────────────────────────

/// <summary>Taxa administrativa descontada do artista no saque (configurável pelo admin).</summary>
public record TaxaAdministrativaDto(decimal Percentual);

public record SaveTaxaAdministrativaRequest(decimal Percentual);

/// <summary>Item do link de pagamento do InfinityPay (show e/ou taxa administrativa).</summary>
public record InfinityPayItem(int PriceCents, string Description);
