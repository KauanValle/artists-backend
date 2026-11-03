using ArtistPlatform.Application.DTOs;

namespace ArtistPlatform.Application.Interfaces;

/// <summary>POST /links da API do InfinityPay — gera o checkout do cachê do show.</summary>
public interface IInfinityPayClient
{
    /// <summary>Cria o link com os itens informados (show + taxa administrativa).</summary>
    Task<InfinityPayLinkResult> CreateLinkAsync(Guid paymentRef, List<InfinityPayItem> items);
}
