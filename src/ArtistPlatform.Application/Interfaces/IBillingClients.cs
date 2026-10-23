using ArtistPlatform.Application.DTOs;

namespace ArtistPlatform.Application.Interfaces;

/// <summary>Cliente HTTP da API de checkouts do Asaas.</summary>
public interface IAsaasCheckoutClient
{
    Task<CheckoutResponseDto> CreateCheckoutAsync(Guid userId);
}

/// <summary>Promove a conta do usuário para Assinante (implementado pelo Identity).</summary>
public interface ISubscriptionActivator
{
    /// <summary>Ativa a assinatura; retorna true se a conta foi alterada nesta chamada.</summary>
    Task<bool> ActivateAsync(Guid userId);
}
