using Pedidos.Domain.ObjetosDeValor;

namespace Pedidos.Domain.Resultados;

public record ResultadoConsolidacao(IReadOnlyList<ItemSolicitado> Itens, IReadOnlyList<int> ProdutosComQuantidadeExcedida)
{
    public bool Valido => ProdutosComQuantidadeExcedida.Count == 0;
}
