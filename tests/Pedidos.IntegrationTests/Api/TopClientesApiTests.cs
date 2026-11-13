using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pedidos.IntegrationTests.Infra;

namespace Pedidos.IntegrationTests.Api;

/// <summary>Cada teste usa um período próprio (anos distantes) para não sofrer interferência dos demais.</summary>
public class TopClientesApiTests(BancoDeTeste banco) : IClassFixture<BancoDeTeste>, IDisposable
{
    private readonly FabricaApi _fabrica = new(banco.StringConexao);

    public void Dispose() => _fabrica.Dispose();

    private static DateTime Utc(int ano, int mes, int dia, int hora = 0, int minuto = 0, int segundo = 0) =>
        new(ano, mes, dia, hora, minuto, segundo, DateTimeKind.Utc);

    private async Task<List<(int ClienteId, string Nome, decimal ValorTotal)>> ObterAsync(string inicio, string fim)
    {
        var resposta = await _fabrica.CreateClient().GetAsync($"/relatorios/top-clientes?dataInicio={inicio}&dataFim={fim}");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        return
        [
            .. corpo.EnumerateArray().Select(e => (
                e.GetProperty("clienteId").GetInt32(),
                e.GetProperty("nome").GetString()!,
                e.GetProperty("valorTotal").GetDecimal())),
        ];
    }

    private async Task<int> PedidoAsync(int clienteId, DateTime data, decimal valor)
    {
        var produto = await banco.InserirProdutoAsync(valor, estoque: 1);
        return await banco.InserirPedidoAsync(clienteId, data, (produto, 1, valor));
    }

    [Trait("CA", "CA-26")]
    [Fact]
    public async Task RetornaExatamenteCincoClientesEmOrdemDecrescenteDeTotal()
    {
        var clientes = new List<int>();
        for (var i = 1; i <= 6; i++)
        {
            var cliente = await banco.InserirClienteAsync($"Cliente {i}");
            clientes.Add(cliente);
            await PedidoAsync(cliente, Utc(2040, 1, 10), i * 100m);
        }

        var ranking = await ObterAsync("2040-01-01", "2040-01-31");

        Assert.Equal(5, ranking.Count);
        Assert.Equal([clientes[5], clientes[4], clientes[3], clientes[2], clientes[1]], ranking.Select(r => r.ClienteId));
        Assert.Equal([600m, 500m, 400m, 300m, 200m], ranking.Select(r => r.ValorTotal));
    }

    [Trait("CA", "CA-27")]
    [Fact]
    public async Task HomonimosAparecemComoEntradasDistintasComTotaisNaoSomados()
    {
        var primeiro = await banco.InserirClienteAsync("João Souza");
        var segundo = await banco.InserirClienteAsync("João Souza");
        await PedidoAsync(primeiro, Utc(2040, 2, 5), 300m);
        await PedidoAsync(primeiro, Utc(2040, 2, 6), 200m);
        await PedidoAsync(segundo, Utc(2040, 2, 7), 400m);

        var ranking = await ObterAsync("2040-02-01", "2040-02-28");

        Assert.Equal([(primeiro, "João Souza", 500m), (segundo, "João Souza", 400m)], ranking);
    }

    [Trait("CA", "CA-28")]
    [Fact]
    public async Task EmpateDeTotalDesempataPorClienteIdCrescenteDeFormaRepetivel()
    {
        var a = await banco.InserirClienteAsync("A");
        var b = await banco.InserirClienteAsync("B");
        var c = await banco.InserirClienteAsync("C");
        // Inseridos fora de ordem de id para provar que o desempate não depende da ordem física.
        await PedidoAsync(c, Utc(2040, 3, 1), 250m);
        await PedidoAsync(a, Utc(2040, 3, 2), 250m);
        await PedidoAsync(b, Utc(2040, 3, 3), 250m);

        var primeira = await ObterAsync("2040-03-01", "2040-03-31");
        var segunda = await ObterAsync("2040-03-01", "2040-03-31");

        Assert.Equal([a, b, c], primeira.Select(r => r.ClienteId));
        Assert.Equal(primeira, segunda);
    }

    [Trait("CA", "CA-29")]
    [Fact]
    public async Task IgnoraPedidosForaDoPeriodoInclusiveUmSegundoApos()
    {
        var cliente = await banco.InserirClienteAsync();
        await PedidoAsync(cliente, Utc(2040, 4, 30, 23, 59, 59), 100m); // último segundo do período
        await PedidoAsync(cliente, Utc(2040, 4, 1), 10m); // primeiro instante do período
        await PedidoAsync(cliente, Utc(2040, 5, 1), 1000m); // 1 segundo depois do fim
        await PedidoAsync(cliente, Utc(2040, 3, 31, 23, 59, 59), 5000m); // 1 segundo antes do início

        var ranking = await ObterAsync("2040-04-01", "2040-04-30");

        Assert.Equal([(cliente, "Cliente de teste", 110m)], ranking);
    }

    [Trait("CA", "CA-30")]
    [Fact]
    public async Task RetornaSomenteOsExistentesEListaVaziaSemPedidos()
    {
        var a = await banco.InserirClienteAsync();
        var b = await banco.InserirClienteAsync();
        await PedidoAsync(a, Utc(2041, 1, 10), 100m);
        await PedidoAsync(b, Utc(2041, 1, 11), 50m);

        var poucos = await ObterAsync("2041-01-01", "2041-01-31");
        var nenhum = await ObterAsync("2042-01-01", "2042-01-31");

        Assert.Equal([a, b], poucos.Select(r => r.ClienteId));
        Assert.Empty(nenhum);
    }

    [Trait("CA", "CA-31")]
    [Theory]
    [InlineData("")]
    [InlineData("?dataInicio=2026-10-01")]
    [InlineData("?dataFim=2026-10-31")]
    [InlineData("?dataInicio=2026-13-40&dataFim=2026-10-31")]
    [InlineData("?dataInicio=01/10/2026&dataFim=31/10/2026")]
    [InlineData("?dataInicio=2026-10-31&dataFim=2026-10-01")]
    public async Task RetornaRequisicaoInvalidaParaPeriodoAusenteInvalidoOuInvertido(string consulta)
    {
        var resposta = await _fabrica.CreateClient().GetAsync("/relatorios/top-clientes" + consulta);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("REQUISICAO_INVALIDA", corpo.GetProperty("codigo").GetString());
    }
}
