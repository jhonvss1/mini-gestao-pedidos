namespace Pedidos.Application.Modelos;

public sealed record ItemPedidoListado(
    int ProdutoId,
    string ProdutoNome,
    int Quantidade,
    decimal PrecoUnitario,
    decimal Subtotal);
