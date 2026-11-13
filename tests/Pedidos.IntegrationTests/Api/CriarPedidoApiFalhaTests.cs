using System.Net;
using System.Net.Http.Json;
using Pedidos.IntegrationTests.Suporte;

namespace Pedidos.IntegrationTests.Api;

public class CriarPedidoApiFalhaTests(BancoDeTeste banco) : IClassFixture<BancoDeTeste>, IDisposable
{
    private readonly FabricaApi _fabrica = new(banco.StringConexao, new FalhaAoSalvarInterceptor());

    public void Dispose() => _fabrica.Dispose();

    [Trait("CA", "CA-13")]
    [Fact]
    public async Task FalhaAposABaixaDeEstoqueReverteTudoERetorna500SemDetalhes()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(10m, estoque: 10);

        var resposta = await _fabrica.CreateClient().PostAsJsonAsync(
            "/pedidos",
            new { clienteId = cliente, itens = new[] { new { produtoId = produto, quantidade = 3 } } });

        Assert.Equal(HttpStatusCode.InternalServerError, resposta.StatusCode);
        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Falha injetada", corpo);
        Assert.DoesNotContain("InvalidOperationException", corpo);
        Assert.Equal(10, await banco.EstoqueAsync(produto));
        Assert.Equal(0, await banco.EscalarAsync<int>($"SELECT COUNT(*) FROM dbo.pedido WHERE cliente_id = {cliente}"));
    }
}
