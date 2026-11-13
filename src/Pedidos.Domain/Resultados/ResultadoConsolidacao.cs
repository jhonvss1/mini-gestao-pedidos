using Pedidos.Domain.ObjetosDeValor;

namespace Pedidos.Domain.Resultados;

public sealed record ResultadoConsolidacao(
    IReadOnlyList<ItemSolicitado> Itens,
    IReadOnlyList<int> ProdutosComQuantidadeExcedida)
{
    public bool Valido => ProdutosComQuantidadeExcedida.Count == 0;
}
