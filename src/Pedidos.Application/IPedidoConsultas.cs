namespace Pedidos.Application;

/// <summary>
/// Porta de leitura implementada pela Infrastructure. Devolve read models, não agregados. Não é CQRS formal:
/// usa o mesmo DbContext, sem barramento nem handlers.
/// </summary>
public interface IPedidoConsultas
{
    /// <summary>Página de pedidos em ordem fixa <c>DataCriacao desc, Id desc</c> (RN-16) e total após os filtros.</summary>
    Task<PedidosEncontrados> ListarAsync(FiltroPedidos filtro, CancellationToken cancellationToken);

    /// <summary>Todos os clientes, ordenados por nome e id.</summary>
    Task<IReadOnlyList<ClienteResumo>> ListarClientesAsync(CancellationToken cancellationToken);

    /// <summary>Todos os produtos com preço e estoque atuais, ordenados por nome e id.</summary>
    Task<IReadOnlyList<ProdutoResumo>> ListarProdutosAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Até <paramref name="limite"/> clientes por soma de <c>ValorTotal</c> no período (início inclusivo, fim exclusivo),
    /// agrupados por id do cliente (RN-19), em ordem decrescente de total e, no empate, crescente de id (RN-20).
    /// </summary>
    Task<IReadOnlyList<ClienteRanking>> ObterTopClientesAsync(
        DateTime inicio,
        DateTime? fimExclusivo,
        int limite,
        CancellationToken cancellationToken);
}
