using Pedidos.Application;

namespace Pedidos.Api.Contratos;

/// <summary>
/// Corpo do <c>POST /pedidos</c>. Campos anuláveis: ausente é 422, tipo errado é 400. Não há campo de preço:
/// qualquer <c>precoUnitario</c> enviado é ignorado na desserialização (RN-04).
/// </summary>
public sealed record CriarPedidoRequisicao(int? ClienteId, List<ItemRequisicao?>? Itens)
{
    public CriarPedidoComando ParaComando() =>
        new(ClienteId, Itens?.Select(i => i is null ? null : new ItemComando(i.ProdutoId, i.Quantidade)).ToList());
}

public sealed record ItemRequisicao(int? ProdutoId, int? Quantidade);
