using System.Net.Http.Json;
using System.Text.Json;
using Pedidos.IntegrationTests.Suporte;

namespace Pedidos.IntegrationTests.Banco;

/// <summary>A carga de demonstração (05) é maior que a de desenvolvimento, mas segue as mesmas regras de consistência.</summary>
public class CargaDemonstracaoTests(BancoDeTeste banco) : IClassFixture<BancoDeTeste>, IAsyncLifetime
{
    private const string Mes = "DATEFROMPARTS(YEAR(data_criacao), MONTH(data_criacao), 1)";

    public Task InitializeAsync() => banco.ExecutarScriptAsync("05-seed-demonstracao.sql");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CarregaOVolumeEsperado()
    {
        Assert.Equal(40, await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.cliente"));
        Assert.Equal(30, await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.produto"));
        Assert.Equal(320, await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.pedido"));
        Assert.True(await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.item_pedido") >= 320);
    }

    [Fact]
    public async Task TodoPedidoTemItensEValorTotalIgualASomaDosItens()
    {
        var semItens = await banco.EscalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.pedido p WHERE NOT EXISTS (SELECT 1 FROM dbo.item_pedido i WHERE i.pedido_id = p.id)");
        var divergentes = await banco.EscalarAsync<int>("""
            SELECT COUNT(*) FROM (
                SELECT p.id FROM dbo.pedido p JOIN dbo.item_pedido i ON i.pedido_id = p.id
                GROUP BY p.id, p.valor_total
                HAVING p.valor_total <> SUM(i.quantidade * i.preco_unitario)) d
            """);

        Assert.Equal(0, semItens);
        Assert.Equal(0, divergentes);
    }

    [Fact]
    public async Task TemHomonimosEProdutosSemEstoqueOuComEstoqueBaixo()
    {
        var homonimos = await banco.EscalarAsync<int>(
            "SELECT COUNT(*) FROM (SELECT nome FROM dbo.cliente GROUP BY nome HAVING COUNT(*) > 1) h");

        Assert.True(homonimos >= 3);
        Assert.True(await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.produto WHERE estoque = 0") >= 2);
        Assert.True(await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.produto WHERE estoque BETWEEN 1 AND 9") >= 3);
    }

    [Fact]
    public async Task EspalhaOsPedidosPorMesesDeJaneiroANovembroSemDatasFuturas()
    {
        var meses = await banco.EscalarAsync<int>($"SELECT COUNT(DISTINCT {Mes}) FROM dbo.pedido");
        var primeiro = await banco.EscalarAsync<DateTime>("SELECT MIN(data_criacao) FROM dbo.pedido");
        var ultimo = await banco.EscalarAsync<DateTime>("SELECT MAX(data_criacao) FROM dbo.pedido");

        Assert.Equal(11, meses);
        Assert.True(primeiro >= new DateTime(2026, 1, 5));
        Assert.True(ultimo < new DateTime(2026, 11, 13));
    }

    [Fact]
    public async Task TodoMesTemSeisOuMaisClientesComPedidosParaOCorteDoRankingSerObservavel()
    {
        var mesesComPoucosClientes = await banco.EscalarAsync<int>($"""
            SELECT COUNT(*) FROM (
                SELECT {Mes} AS mes FROM dbo.pedido GROUP BY {Mes} HAVING COUNT(DISTINCT cliente_id) < 6) m
            """);

        Assert.Equal(0, mesesComPoucosClientes);
    }

    [Fact]
    public async Task PedidosAntigosPreservamOPrecoDeVendaDiferenteDoPrecoAtual()
    {
        var diferentes = await banco.EscalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.item_pedido i JOIN dbo.produto p ON p.id = i.produto_id WHERE i.preco_unitario <> p.preco");

        Assert.True(diferentes > 0);
    }

    [Fact]
    public async Task ReexecutarNaoDuplicaNemFalha()
    {
        await banco.ExecutarScriptAsync("05-seed-demonstracao.sql");

        Assert.Equal(40, await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.cliente"));
        Assert.Equal(320, await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.pedido"));
    }

    [Fact]
    public async Task ApiListaOsPedidosEMontaORankingComOsDadosDaCarga()
    {
        using var fabrica = new FabricaApi(banco.StringConexao);
        var http = fabrica.CreateClient();

        var lista = await http.GetFromJsonAsync<JsonElement>("/api/pedidos?pagina=1&tamanhoPagina=10");
        var ranking = await http.GetFromJsonAsync<JsonElement>("/api/relatorios/top-clientes?dataInicio=2026-10-01&dataFim=2026-10-31");

        Assert.Equal(320, lista.GetProperty("totalItens").GetInt32());
        Assert.Equal(10, lista.GetProperty("registros").GetArrayLength());
        Assert.Equal(5, ranking.GetArrayLength());
    }
}
