using Pedidos.IntegrationTests.Suporte;

namespace Pedidos.IntegrationTests.Banco;

/// <summary>Banco próprio: o guard da carga é "tabela cliente vazia", então com dados existentes nada novo entra.</summary>
public class CargaDemonstracaoSobreDadosExistentesTests(BancoDeTeste banco) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task NaoSobrescreveNemMisturaComDadosExistentes()
    {
        await banco.InserirClienteAsync("Cliente existente");

        await banco.ExecutarScriptAsync("05-seed-demonstracao.sql");

        Assert.Equal(1, await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.cliente"));
        Assert.Equal(0, await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.pedido"));
        Assert.Equal(0, await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.produto"));
    }
}
