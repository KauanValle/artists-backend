using ArtistPlatform.Application.DTOs;

namespace ArtistPlatform.Application.Interfaces;

/// <summary>POST /links da API do InfinityPay — gera o checkout do cachê do show.</summary>
public interface IInfinityPayClient
{
    /// <summary>Cria o link com os itens informados (show + taxa administrativa).</summary>
    Task<InfinityPayLinkResult> CreateLinkAsync(Guid paymentRef, List<InfinityPayItem> items);

    /// <summary>
    /// Consulta o status do pagamento no InfinityPay (POST /payment_check) pelo order_nsu do link.
    /// Plano B quando o webhook não chega. Implementações que não suportam retornam false.
    /// </summary>
    Task<bool> IsPaidAsync(string orderNsu, string? transactionNsu = null, string? slug = null)
        => Task.FromResult(false);
}
