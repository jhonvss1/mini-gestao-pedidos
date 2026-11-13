using System.Net;
using Pedidos.IntegrationTests.Suporte;

namespace Pedidos.IntegrationTests.Api;

public class SaudeApiTests(BancoDeTeste banco) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task RetornaSaudavelQuandoOBancoResponde()
    {
        using var fabrica = new FabricaApi(banco.StringConexao);

        var resposta = await fabrica.CreateClient().GetAsync("/api/saude");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Healthy", await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RetornaIndisponivelQuandoOBancoNaoResponde()
    {
        // Porta sem nada escutando e timeout curto: prova que o endpoint consulta o banco de verdade.
        using var fabrica = new FabricaApi("Server=localhost,1;Database=inexistente;User Id=sa;Password=x;TrustServerCertificate=True;Connect Timeout=2");

        var resposta = await fabrica.CreateClient().GetAsync("/api/saude");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resposta.StatusCode);
        Assert.Equal("Unhealthy", await resposta.Content.ReadAsStringAsync());
    }
}
