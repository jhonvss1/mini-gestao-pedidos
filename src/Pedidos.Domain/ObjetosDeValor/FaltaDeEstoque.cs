namespace Pedidos.Domain.ObjetosDeValor;

/// <summary>Produto sem saldo suficiente, com a quantidade pedida (consolidada) e o saldo lido.</summary>
public sealed record FaltaDeEstoque(int ProdutoId, int Solicitado, int Disponivel);
