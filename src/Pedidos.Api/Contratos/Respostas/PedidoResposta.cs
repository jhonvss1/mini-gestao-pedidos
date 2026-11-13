using Pedidos.Domain.Entidades;

namespace Pedidos.Api.Contratos.Respostas;

public record PedidoResposta(
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
