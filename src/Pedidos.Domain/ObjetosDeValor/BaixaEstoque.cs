namespace Pedidos.Domain.ObjetosDeValor;

/// <summary>Débito de estoque que a persistência deve aplicar de forma atômica (ordem crescente de produto).</summary>
public sealed record BaixaEstoque(int ProdutoId, int Quantidade);
