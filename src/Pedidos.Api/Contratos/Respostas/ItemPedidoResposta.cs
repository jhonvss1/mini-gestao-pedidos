namespace Pedidos.Api.Contratos.Respostas;

public record ItemPedidoResposta(int ProdutoId, int Quantidade, decimal PrecoUnitario, decimal Subtotal);
