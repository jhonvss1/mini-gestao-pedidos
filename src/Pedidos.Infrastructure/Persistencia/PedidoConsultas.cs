using Microsoft.EntityFrameworkCore;
using Pedidos.Application;
using Pedidos.Domain.Entidades;

namespace Pedidos.Infrastructure.Persistencia;

/// <summary>Consultas de leitura (sem rastreamento), projetadas direto para os read models de Application.</summary>
public sealed class PedidoConsultas(PedidosDbContext contexto) : IPedidoConsultas
{
    public async Task<IReadOnlyList<ClienteResumo>> ListarClientesAsync(CancellationToken cancellationToken) =>
        await contexto.Clientes
            .AsNoTracking()
            .OrderBy(c => c.Nome).ThenBy(c => c.Id)
            .Select(c => new ClienteResumo(c.Id, c.Nome))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProdutoResumo>> ListarProdutosAsync(CancellationToken cancellationToken) =>
        await contexto.Produtos
            .AsNoTracking()
            .OrderBy(p => p.Nome).ThenBy(p => p.Id)
            .Select(p => new ProdutoResumo(p.Id, p.Nome, p.Preco, p.Estoque))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ClienteRanking>> ObterTopClientesAsync(
        DateTime inicio,
        DateTime? fimExclusivo,
        int limite,
        CancellationToken cancellationToken)
    {
        var pedidos = contexto.Pedidos.AsNoTracking().Where(p => p.DataCriacao >= inicio);
        if (fimExclusivo is { } fim)
            pedidos = pedidos.Where(p => p.DataCriacao < fim);

        // Agrega primeiro por id do cliente (nunca por nome, RN-19) e só depois busca o nome dos poucos selecionados.
        var topo = await pedidos
            .GroupBy(p => p.ClienteId)
            .Select(g => new { ClienteId = g.Key, ValorTotal = g.Sum(p => p.ValorTotal) })
            .OrderByDescending(x => x.ValorTotal)
            .ThenBy(x => x.ClienteId)
            .Take(limite)
            .ToListAsync(cancellationToken);

        var ids = topo.Select(x => x.ClienteId).ToList();
        var nomes = await contexto.Clientes
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Nome, cancellationToken);

        return [.. topo.Select(x => new ClienteRanking(x.ClienteId, nomes[x.ClienteId], x.ValorTotal))];
    }

    public async Task<PedidosEncontrados> ListarAsync(FiltroPedidos filtro, CancellationToken cancellationToken)
    {
        var pedidos = contexto.Pedidos.AsNoTracking();
        if (filtro.ClienteId is { } clienteId)
            pedidos = pedidos.Where(p => p.ClienteId == clienteId);
        if (filtro.Inicio is { } inicio)
            pedidos = pedidos.Where(p => p.DataCriacao >= inicio);
        if (filtro.FimExclusivo is { } fimExclusivo)
            pedidos = pedidos.Where(p => p.DataCriacao < fimExclusivo);

        var total = await pedidos.CountAsync(cancellationToken);

        // OFFSET além do int só pode cair depois da última página.
        var deslocamento = (long)(filtro.Pagina - 1) * filtro.TamanhoPagina;
        if (deslocamento >= total)
            return new PedidosEncontrados([], total);

        // Etapa 1: cabeçalhos da página, em ordem fixa e estável (RN-16), com o nome do cliente.
        var cabecalhos = await (
                from p in pedidos
                join c in contexto.Clientes.AsNoTracking() on p.ClienteId equals c.Id
                orderby p.DataCriacao descending, p.Id descending
                select new { p.Id, p.ClienteId, ClienteNome = c.Nome, p.DataCriacao, p.ValorTotal })
            .Skip((int)deslocamento)
            .Take(filtro.TamanhoPagina)
            .ToListAsync(cancellationToken);

        // Etapa 2: itens dos pedidos da página, com o nome do produto.
        var pedidoIds = cabecalhos.Select(c => c.Id).ToList();
        var itens = await (
                from i in contexto.Set<ItemPedido>().AsNoTracking()
                join pr in contexto.Produtos.AsNoTracking() on i.ProdutoId equals pr.Id
                where pedidoIds.Contains(EF.Property<int>(i, "PedidoId"))
                orderby i.ProdutoId
                select new
                {
                    PedidoId = EF.Property<int>(i, "PedidoId"),
                    Item = new ItemPedidoListado(i.ProdutoId, pr.Nome, i.Quantidade, i.PrecoUnitario, i.Quantidade * i.PrecoUnitario),
                })
            .ToListAsync(cancellationToken);

        var itensPorPedido = itens.ToLookup(x => x.PedidoId, x => x.Item);
        var registros = cabecalhos
            .Select(c => new PedidoListado(
                c.Id,
                c.ClienteId,
                c.ClienteNome,
                c.DataCriacao,
                c.ValorTotal,
                [.. itensPorPedido[c.Id]]))
            .ToList();

        return new PedidosEncontrados(registros, total);
    }
}
