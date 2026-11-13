using Pedidos.IntegrationTests.Infra;

namespace Pedidos.IntegrationTests.Banco;

/// <summary>Mínimos da carga inicial (spec 3.3), conferidos por consultas SQL.</summary>
public class CargaInicialTests(BancoDeTeste banco) : IClassFixture<BancoDeTeste>, IAsyncLifetime
{
    // Mês de cada pedido, para agrupar "períodos".
    private const string Periodo = "DATEFROMPARTS(YEAR(data_criacao), MONTH(data_criacao), 1)";

    public Task InitializeAsync() => banco.ExecutarScriptAsync("02-seed.sql");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("CA", "CA-35")]
    public async Task Tem_ao_menos_7_clientes_com_dois_homonimos_distintos()
    {
        Assert.True(await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.cliente") >= 7);
        Assert.True(await banco.EscalarAsync<int>(
            "SELECT COUNT(*) FROM (SELECT nome FROM dbo.cliente GROUP BY nome HAVING COUNT(*) > 1) h") >= 1);
    }

    [Fact]
    [Trait("CA", "CA-35")]
    public async Task Tem_ao_menos_6_produtos_com_estoque_baixo_e_estoque_zero()
    {
        Assert.True(await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.produto") >= 6);
        Assert.True(await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.produto WHERE estoque = 5") >= 1);
        Assert.True(await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.produto WHERE estoque = 0") >= 1);
    }

    [Fact]
    [Trait("CA", "CA-35")]
    public async Task Tem_6_ou_mais_clientes_com_pedidos_no_mesmo_periodo_em_dois_periodos()
    {
        var periodosComSeisClientes = await banco.EscalarAsync<int>($"""
            SELECT COUNT(*) FROM (
                SELECT {Periodo} AS periodo FROM dbo.pedido
                GROUP BY {Periodo} HAVING COUNT(DISTINCT cliente_id) >= 6) p
            """);

        Assert.True(periodosComSeisClientes >= 2);
    }

    [Fact]
    [Trait("CA", "CA-35")]
    public async Task Tem_empate_de_total_entre_clientes_no_mesmo_periodo()
    {
        var empates = await banco.EscalarAsync<int>($"""
            SELECT COUNT(*) FROM (
                SELECT periodo, total FROM (
                    SELECT {Periodo} AS periodo, cliente_id, SUM(valor_total) AS total
                    FROM dbo.pedido GROUP BY {Periodo}, cliente_id) t
                GROUP BY periodo, total HAVING COUNT(*) > 1) e
            """);

        Assert.True(empates >= 1);
    }

    [Fact]
    [Trait("CA", "CA-35")]
    public async Task Todo_pedido_tem_itens_e_valor_total_igual_a_soma_dos_itens()
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
}
