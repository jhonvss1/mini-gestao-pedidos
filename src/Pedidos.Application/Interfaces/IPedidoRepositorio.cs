using Pedidos.Domain.Entidades;
using Pedidos.Domain.ObjetosDeValor;
using Pedidos.Domain.Resultados;

namespace Pedidos.Application.Interfaces;

/// <summary>
/// Porta de escrita implementada pela Infrastructure. Existe para a Application não enxergar o EF Core e para
/// testar a orquestração das etapas sem banco.
/// </summary>
public interface IPedidoRepositorio
{
    Task<bool> ClienteExisteAsync(int clienteId, CancellationToken cancellationToken);

    /// <summary>
    /// Abre a transação, lê o snapshot dos produtos dos <paramref name="itens"/>, invoca <paramref name="decidir"/>
    /// (a decisão de domínio, pura e reexecutável) e, se ela devolver <see cref="ResultadoCriacaoPedido.Criado"/>,
    /// aplica as baixas de estoque de forma atômica e grava pedido e itens. Qualquer outro desfecho não grava nada.
    /// </summary>
    Task<ResultadoCriacaoPedido> CriarAsync(
        int clienteId,
        IReadOnlyList<ItemSolicitado> itens,
        Func<IReadOnlyCollection<Produto>, ResultadoCriacaoPedido> decidir,
        CancellationToken cancellationToken);
}
