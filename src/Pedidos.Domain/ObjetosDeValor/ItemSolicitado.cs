using Pedidos.Domain.Constantes;
using Pedidos.Domain.Resultados;

namespace Pedidos.Domain.ObjetosDeValor;

/// <summary>Produto e quantidade pedidos, ainda sem preço (o preço vem do cadastro, RN-04).</summary>
public sealed record ItemSolicitado
{
    public ItemSolicitado(int produtoId, int quantidade)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(produtoId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantidade);

        ProdutoId = produtoId;
        Quantidade = quantidade;
    }

    public int ProdutoId { get; }

    public int Quantidade { get; }

    /// <summary>
    /// Une itens do mesmo produto somando as quantidades (RN-09). O resultado sai em ordem crescente de
    /// <see cref="ProdutoId"/>, que é também a ordem de débito do estoque. Somas acima de
    /// <see cref="Limites.QuantidadeMaxima"/> não geram item: o produto é listado em
    /// <see cref="ResultadoConsolidacao.ProdutosComQuantidadeExcedida"/> (RN-24).
    /// </summary>
    public static ResultadoConsolidacao Consolidar(IEnumerable<ItemSolicitado> itens)
    {
        ArgumentNullException.ThrowIfNull(itens);

        var consolidados = new List<ItemSolicitado>();
        var excedidos = new List<int>();

        foreach (var grupo in itens.GroupBy(i => i.ProdutoId).OrderBy(g => g.Key))
        {
            var soma = grupo.Sum(i => (long)i.Quantidade);
            if (soma > Limites.QuantidadeMaxima)
                excedidos.Add(grupo.Key);
            else
                consolidados.Add(new ItemSolicitado(grupo.Key, (int)soma));
        }

        return new ResultadoConsolidacao(consolidados, excedidos);
    }
}
