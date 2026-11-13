namespace Pedidos.Application.Modelos;

public sealed record PedidoListado(
    int Id,
    int ClienteId,
    string ClienteNome,
    DateTime DataCriacao,
    decimal ValorTotal,
    IReadOnlyList<ItemPedidoListado> Itens);
