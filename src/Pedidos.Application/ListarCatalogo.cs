namespace Pedidos.Application;

public sealed record ClienteResumo(int Id, string Nome);

public sealed record ProdutoResumo(int Id, string Nome, decimal Preco, int Estoque);

/// <summary>Leituras sem filtro nem paginação que alimentam os seletores do frontend (spec 4.4).</summary>
public sealed class ListarCatalogo(IPedidoConsultas consultas)
{
    public Task<IReadOnlyList<ClienteResumo>> ClientesAsync(CancellationToken cancellationToken) =>
        consultas.ListarClientesAsync(cancellationToken);

    public Task<IReadOnlyList<ProdutoResumo>> ProdutosAsync(CancellationToken cancellationToken) =>
        consultas.ListarProdutosAsync(cancellationToken);
}
