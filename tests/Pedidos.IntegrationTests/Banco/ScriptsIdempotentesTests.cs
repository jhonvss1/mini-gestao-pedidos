using Pedidos.IntegrationTests.Suporte;

namespace Pedidos.IntegrationTests.Banco;

public class ScriptsIdempotentesTests(BancoDeTeste banco) : IClassFixture<BancoDeTeste>
{
    [Fact]
    [Trait("CA", "CA-34")]
    public async Task ReexecucaoNaoFalhaNemDuplicaDados()
    {
        await banco.ExecutarScriptAsync("02-seed.sql");
        var antes = await ContagensAsync();

        await banco.ExecutarScriptAsync("01-schema.sql");
        await banco.ExecutarScriptAsync("02-seed.sql");
        var depois = await ContagensAsync();

        Assert.Equal(antes, depois);
        Assert.True(antes.Clientes > 0 && antes.Itens > 0);
    }

    private async Task<(int Clientes, int Produtos, int Pedidos, int Itens)> ContagensAsync() => (
        await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.cliente"),
        await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.produto"),
        await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.pedido"),
        await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.item_pedido"));
}
