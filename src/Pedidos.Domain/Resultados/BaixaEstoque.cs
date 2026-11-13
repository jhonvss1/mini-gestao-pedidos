namespace Pedidos.Domain.Resultados;

/// <summary>Débito de estoque que a persistência deve aplicar de forma atômica (ordem crescente de produto).</summary>
public record BaixaEstoque(int ProdutoId, int Quantidade);
