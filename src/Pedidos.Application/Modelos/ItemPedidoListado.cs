namespace Pedidos.Application.Modelos;

public record ItemPedidoListado(
    int ProdutoId,
    string ProdutoNome,
    int Quantidade,
    decimal PrecoUnitario,
    decimal Subtotal);
