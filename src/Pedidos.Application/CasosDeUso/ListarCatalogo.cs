using Pedidos.Application.Interfaces;
using Pedidos.Application.Modelos;

namespace Pedidos.Application.CasosDeUso;

/// <summary>Leituras sem filtro nem paginação que alimentam os seletores do frontend (spec 4.4).</summary>
public sealed class ListarCatalogo(IPedidoConsultas consultas)
{
    public Task<IReadOnlyList<ClienteResumo>> ClientesAsync(CancellationToken cancellationToken) =>
        consultas.ListarClientesAsync(cancellationToken);

    public Task<IReadOnlyList<ProdutoResumo>> ProdutosAsync(CancellationToken cancellationToken) =>
        consultas.ListarProdutosAsync(cancellationToken);
}
