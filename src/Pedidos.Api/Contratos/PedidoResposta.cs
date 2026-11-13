using Pedidos.Domain;

namespace Pedidos.Api.Contratos;

public sealed record PedidoResposta(
    int Id,
    int ClienteId,
    DateTime DataCriacao,
    decimal ValorTotal,
    IReadOnlyList<ItemPedidoResposta> Itens)
{
    public static PedidoResposta De(Pedido pedido) => new(
        pedido.Id,
        pedido.ClienteId,
        pedido.DataCriacao,
        pedido.ValorTotal,
        pedido.Itens.Select(i => new ItemPedidoResposta(i.ProdutoId, i.Quantidade, i.PrecoUnitario, i.Subtotal)).ToList());
}

public sealed record ItemPedidoResposta(int ProdutoId, int Quantidade, decimal PrecoUnitario, decimal Subtotal);
