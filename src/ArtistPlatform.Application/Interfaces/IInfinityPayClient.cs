using ArtistPlatform.Application.DTOs;

namespace ArtistPlatform.Application.Interfaces;

/// <summary>POST /links da API do InfinityPay — gera o checkout do cachê do show.</summary>
public interface IInfinityPayClient
{
    Task<InfinityPayLinkResult> CreateLinkAsync(InfinityPayLinkRequest request);
}
