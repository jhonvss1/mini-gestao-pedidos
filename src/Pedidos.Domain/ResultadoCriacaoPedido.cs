namespace Pedidos.Domain;

/// <summary>Débito de estoque que a persistência deve aplicar de forma atômica (ordem crescente de produto).</summary>
public sealed record BaixaEstoque(int ProdutoId, int Quantidade);

/// <summary>Produto sem saldo suficiente, com a quantidade pedida (consolidada) e o saldo lido.</summary>
public sealed record FaltaDeEstoque(int ProdutoId, int Solicitado, int Disponivel);

/// <summary>Desfecho de <see cref="Pedido.Criar"/>: tipo fechado, sem exceções para fluxo de negócio.</summary>
public abstract record ResultadoCriacaoPedido
{
    private ResultadoCriacaoPedido()
    {
    }

    public sealed record Criado(Pedido Pedido, IReadOnlyList<BaixaEstoque> Baixas) : ResultadoCriacaoPedido;

    public sealed record ProdutosInexistentes(IReadOnlyList<int> ProdutoIds) : ResultadoCriacaoPedido;

    /// <summary>Subtotais ou total acima de <see cref="Limites.ValorMonetarioMaximo"/> (RN-24).</summary>
    public sealed record LimiteExcedido(IReadOnlyList<int> ProdutosComSubtotalExcedido, bool TotalExcedido)
        : ResultadoCriacaoPedido;

    public sealed record EstoqueInsuficiente(IReadOnlyList<FaltaDeEstoque> Produtos) : ResultadoCriacaoPedido;
}
