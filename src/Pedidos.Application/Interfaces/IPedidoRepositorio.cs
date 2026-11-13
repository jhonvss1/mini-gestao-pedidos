using Pedidos.Domain.Entidades;
using Pedidos.Domain.ObjetosDeValor;
using Pedidos.Domain.Resultados;

namespace Pedidos.Application.Interfaces;

public interface IPedidoRepositorio
{
    Task<bool> ClienteExisteAsync(int clienteId, CancellationToken cancellationToken);

    Task<ResultadoCriacaoPedido> CriarAsync(
        int clienteId,
        IReadOnlyList<ItemSolicitado> itens,
        Func<IReadOnlyCollection<Produto>, ResultadoCriacaoPedido> decidir,
        CancellationToken cancellationToken);
}
