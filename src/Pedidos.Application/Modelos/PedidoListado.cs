namespace Pedidos.Application.Modelos;

public record PedidoListado(
    int Id,
    int ClienteId,
    string ClienteNome,
    DateTime DataCriacao,
    decimal ValorTotal,
    IReadOnlyList<ItemPedidoListado> Itens);
