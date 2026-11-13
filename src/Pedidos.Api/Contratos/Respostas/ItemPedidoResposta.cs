namespace Pedidos.Api.Contratos.Respostas;

public sealed record ItemPedidoResposta(int ProdutoId, int Quantidade, decimal PrecoUnitario, decimal Subtotal);
