namespace Pedidos.Domain.Resultados;

/// <summary>Produto sem saldo suficiente, com a quantidade pedida (consolidada) e o saldo lido.</summary>
public record FaltaDeEstoque(int ProdutoId, int Solicitado, int Disponivel);
