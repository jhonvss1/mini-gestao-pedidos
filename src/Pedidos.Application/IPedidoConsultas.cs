namespace Pedidos.Application;

/// <summary>
/// Porta de leitura implementada pela Infrastructure. Devolve read models, não agregados. Não é CQRS formal:
/// usa o mesmo DbContext, sem barramento nem handlers.
/// </summary>
public interface IPedidoConsultas
{
    /// <summary>Página de pedidos em ordem fixa <c>DataCriacao desc, Id desc</c> (RN-16) e total após os filtros.</summary>
    Task<PedidosEncontrados> ListarAsync(FiltroPedidos filtro, CancellationToken cancellationToken);
}
