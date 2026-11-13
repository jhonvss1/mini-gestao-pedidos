using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pedidos.Application;
using Pedidos.Domain.Entidades;
using Pedidos.Infrastructure.Persistencia;
using Pedidos.IntegrationTests.Infra;

namespace Pedidos.IntegrationTests.Api;

/// <summary>Requisições HTTP simultâneas e reais contra o SQL Server: estoque nunca negativo e sem 5xx (RN-08).</summary>
public class ConcorrenciaApiTests(BancoDeTeste banco) : IClassFixture<BancoDeTeste>, IDisposable
{
    private const int Rodadas = 20;

    private readonly FabricaApi _fabrica = new(banco.StringConexao);

    // Mesma API, mas com retry de deadlock desligado: se a ordenação do débito não bastasse, o 1205 viraria 500.
    private readonly FabricaApi _fabricaSemRetryDeDeadlock = new(banco.StringConexao)
    {
        ConfigurarServicos = servicos =>
        {
            servicos.RemoveAll<IPedidoRepositorio>();
            servicos.AddScoped<IPedidoRepositorio>(provedor =>
                new PedidoRepositorio(provedor.GetRequiredService<PedidosDbContext>(), maximoTentativasPorDeadlock: 1));
        },
    };

    public void Dispose()
    {
        _fabrica.Dispose();
        _fabricaSemRetryDeDeadlock.Dispose();
    }

    /// <summary>Dispara todas as requisições de uma vez, liberadas juntas por uma barreira de largada.</summary>
    private static async Task<List<HttpStatusCode>> DispararAsync(FabricaApi fabrica, IEnumerable<object> corpos)
    {
        var largada = new TaskCompletionSource();
        var cliente = fabrica.CreateClient();

        var tarefas = corpos
            .Select(async corpo =>
            {
                await largada.Task;
                using var resposta = await cliente.PostAsJsonAsync("/pedidos", corpo);
                return resposta.StatusCode;
            })
            .ToList();

        largada.SetResult();
        return [.. await Task.WhenAll(tarefas)];
    }

    private static object Pedido(int clienteId, params (int ProdutoId, int Quantidade)[] itens) => new
    {
        clienteId,
        itens = itens.Select(i => new { produtoId = i.ProdutoId, quantidade = i.Quantidade }),
    };

    private Task<int> SomaVendidaAsync(int produtoId) =>
        banco.EscalarAsync<int>($"SELECT COALESCE(SUM(quantidade), 0) FROM dbo.item_pedido WHERE produto_id = {produtoId}");

    [Trait("CA", "CA-14")]
    [Fact]
    public async Task VintePedidosDeUmaUnidadeParaEstoque10GeramExatamente10Vendas()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(10m, estoque: 10);

        var status = await DispararAsync(_fabrica, Enumerable.Range(0, 20).Select(_ => Pedido(cliente, (produto, 1))));

        Assert.Equal(10, status.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(10, status.Count(s => s == HttpStatusCode.Conflict));
        Assert.DoesNotContain(status, s => (int)s >= 500);
        Assert.Equal(0, await banco.EstoqueAsync(produto));
        Assert.Equal(10, await SomaVendidaAsync(produto));
    }

    [Trait("CA", "CA-15")]
    [Fact]
    public async Task DoisPedidosDeSeisParaEstoque10GeramUmaVendaEUmConflito()
    {
        var cliente = await banco.InserirClienteAsync();

        for (var rodada = 0; rodada < Rodadas; rodada++)
        {
            var produto = await banco.InserirProdutoAsync(10m, estoque: 10);

            var status = await DispararAsync(_fabrica, [Pedido(cliente, (produto, 6)), Pedido(cliente, (produto, 6))]);

            Assert.Equal(1, status.Count(s => s == HttpStatusCode.Created));
            Assert.Equal(1, status.Count(s => s == HttpStatusCode.Conflict));
            Assert.Equal(4, await banco.EstoqueAsync(produto));
        }
    }

    [Trait("CA", "CA-16")]
    [Fact]
    public async Task PedidosComOsMesmosProdutosEmOrdensOpostasNaoGeramErro5xxNemEstoqueNegativo()
    {
        await ExecutarRodadasMultiProdutoAsync(_fabrica);
    }

    [Trait("CA", "CA-16")]
    [Fact]
    public async Task AOrdenacaoDoDebitoEvitaDeadlockMesmoSemRetry()
    {
        await ExecutarRodadasMultiProdutoAsync(_fabricaSemRetryDeDeadlock);
    }

    private async Task ExecutarRodadasMultiProdutoAsync(FabricaApi fabrica)
    {
        var cliente = await banco.InserirClienteAsync();

        for (var rodada = 0; rodada < Rodadas; rodada++)
        {
            const int estoqueInicial = 15;
            var a = await banco.InserirProdutoAsync(10m, estoque: estoqueInicial);
            var b = await banco.InserirProdutoAsync(10m, estoque: estoqueInicial);

            // 30 pedidos disputam 15 unidades de cada produto, metade pedindo (A, B) e metade (B, A).
            var corpos = Enumerable.Range(0, 30)
                .Select(i => i % 2 == 0 ? Pedido(cliente, (a, 1), (b, 1)) : Pedido(cliente, (b, 1), (a, 1)));

            var status = await DispararAsync(fabrica, corpos);

            Assert.DoesNotContain(status, s => (int)s >= 500);
            Assert.Equal(estoqueInicial, status.Count(s => s == HttpStatusCode.Created));
            Assert.Equal(15, status.Count(s => s == HttpStatusCode.Conflict));
            Assert.Equal(0, await banco.EstoqueAsync(a));
            Assert.Equal(0, await banco.EstoqueAsync(b));
            Assert.Equal(estoqueInicial, await SomaVendidaAsync(a));
            Assert.Equal(estoqueInicial, await SomaVendidaAsync(b));
        }
    }
}
