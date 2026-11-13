using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pedidos.Application;
using Pedidos.Domain.Entidades;
using Pedidos.Domain.ObjetosDeValor;
using Pedidos.Domain.Resultados;

namespace Pedidos.Infrastructure.Persistencia;

/// <summary>
/// Cria o pedido em uma única transação. A regra de estoque é decidida pelo domínio sobre um snapshot; a garantia
/// sob concorrência é o <c>UPDATE</c> condicional (<c>estoque &gt;= @q</c>) aplicado em ordem crescente de id:
/// zero linhas afetadas significa que a disputa foi perdida, e tudo é revertido.
/// </summary>
/// <param name="contexto">Contexto da requisição.</param>
/// <param name="maximoTentativasPorDeadlock">
/// Tentativas totais ao ser vítima de deadlock (1205). Use 1 nos testes para provar que a ordenação do débito, sozinha,
/// evita deadlock.
/// </param>
public sealed class PedidoRepositorio(PedidosDbContext contexto, int maximoTentativasPorDeadlock = 3) : IPedidoRepositorio
{
    private const int MaximoTentativasPorDisputa = 3;
    private const int ErroDeadlock = 1205;

    public Task<bool> ClienteExisteAsync(int clienteId, CancellationToken cancellationToken) =>
        contexto.Clientes.AsNoTracking().AnyAsync(c => c.Id == clienteId, cancellationToken);

    public async Task<ResultadoCriacaoPedido> CriarAsync(
        int clienteId,
        IReadOnlyList<ItemSolicitado> itens,
        Func<IReadOnlyCollection<Produto>, ResultadoCriacaoPedido> decidir,
        CancellationToken cancellationToken)
    {
        // Defesa em profundidade: a ordem crescente de débito evita deadlock; se ele ocorrer, a transação é repetida.
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                return await CriarEmTransacaoAsync(itens, decidir, cancellationToken);
            }
            catch (Exception ex) when (EhDeadlock(ex) && tentativa < maximoTentativasPorDeadlock)
            {
                contexto.ChangeTracker.Clear();
            }
        }
    }

    private async Task<ResultadoCriacaoPedido> CriarEmTransacaoAsync(
        IReadOnlyList<ItemSolicitado> itens,
        Func<IReadOnlyCollection<Produto>, ResultadoCriacaoPedido> decidir,
        CancellationToken cancellationToken)
    {
        var produtoIds = itens.Select(i => i.ProdutoId).ToList();

        for (var disputa = 1; ; disputa++)
        {
            await using var transacao = await contexto.Database.BeginTransactionAsync(cancellationToken);

            var snapshot = await contexto.Produtos
                .AsNoTracking()
                .Where(p => produtoIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

            var decisao = decidir(snapshot);
            if (decisao is not ResultadoCriacaoPedido.Criado criado)
                return decisao; // a transação é descartada ao sair: nada foi escrito

            var perdida = await DebitarEstoqueAsync(criado.Baixas, cancellationToken);
            if (perdida is null)
            {
                contexto.Pedidos.Add(criado.Pedido);
                await contexto.SaveChangesAsync(cancellationToken);
                await transacao.CommitAsync(cancellationToken);
                return criado;
            }

            // Perdeu a disputa: desfaz inclusive os débitos anteriores deste pedido e reavalia sobre o saldo real.
            await transacao.RollbackAsync(cancellationToken);
            contexto.ChangeTracker.Clear();

            if (disputa == MaximoTentativasPorDisputa)
                return await FaltaAposDisputaAsync(perdida, cancellationToken);
        }
    }

    /// <summary>Aplica as baixas em ordem crescente de produto. Devolve a baixa que perdeu a disputa, ou null.</summary>
    private async Task<BaixaEstoque?> DebitarEstoqueAsync(
        IReadOnlyList<BaixaEstoque> baixas,
        CancellationToken cancellationToken)
    {
        foreach (var baixa in baixas.OrderBy(b => b.ProdutoId))
        {
            var linhas = await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE produto SET estoque = estoque - {baixa.Quantidade}
                WHERE id = {baixa.ProdutoId} AND estoque >= {baixa.Quantidade}
                """,
                cancellationToken);

            if (linhas == 0)
                return baixa;
        }

        return null;
    }

    // O estoque só diminui, então a próxima leitura já reflete a falta; este desfecho cobre o caso de esgotar as tentativas.
    private async Task<ResultadoCriacaoPedido> FaltaAposDisputaAsync(BaixaEstoque baixa, CancellationToken cancellationToken)
    {
        var disponivel = await contexto.Produtos
            .AsNoTracking()
            .Where(p => p.Id == baixa.ProdutoId)
            .Select(p => p.Estoque)
            .SingleAsync(cancellationToken);

        return new ResultadoCriacaoPedido.EstoqueInsuficiente(
            [new FaltaDeEstoque(baixa.ProdutoId, baixa.Quantidade, disponivel)]);
    }

    private static bool EhDeadlock(Exception ex)
    {
        for (var atual = ex; atual is not null; atual = atual.InnerException)
        {
            if (atual is SqlException { Number: ErroDeadlock })
                return true;
        }

        return false;
    }
}
