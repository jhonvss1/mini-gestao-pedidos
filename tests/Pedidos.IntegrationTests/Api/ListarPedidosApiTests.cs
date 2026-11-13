using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pedidos.IntegrationTests.Suporte;

namespace Pedidos.IntegrationTests.Api;

public class ListarPedidosApiTests(BancoDeTeste banco) : IClassFixture<BancoDeTeste>, IDisposable
{
    private readonly FabricaApi _fabrica = new(banco.StringConexao);

    public void Dispose() => _fabrica.Dispose();

    private static DateTime Utc(int ano, int mes, int dia, int hora = 0, int minuto = 0, int segundo = 0) =>
        new(ano, mes, dia, hora, minuto, segundo, DateTimeKind.Utc);

    private async Task<HttpResponseMessage> GetAsync(string consulta) =>
        await _fabrica.CreateClient().GetAsync("/api/pedidos" + consulta);

    private async Task<JsonElement> ListarAsync(string consulta)
    {
        var resposta = await GetAsync(consulta);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static List<int> Ids(JsonElement pagina) =>
        [.. pagina.GetProperty("registros").EnumerateArray().Select(r => r.GetProperty("id").GetInt32())];

    [Trait("CA", "CA-18")]
    [Fact]
    public async Task SemFiltrosRetornaPrimeiraPaginaComTotalCorretoEOrdemFixa()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(10m, estoque: 100);
        for (var i = 0; i < 25; i++)
            await banco.InserirPedidoAsync(cliente, Utc(2026, 1, 1).AddMinutes(i), (produto, 1, 10m));

        var pagina = await ListarAsync("");

        Assert.Equal(1, pagina.GetProperty("pagina").GetInt32());
        Assert.Equal(20, pagina.GetProperty("tamanhoPagina").GetInt32());
        Assert.True(pagina.GetProperty("registros").GetArrayLength() <= 20);
        Assert.Equal(
            await banco.EscalarAsync<int>("SELECT COUNT(*) FROM dbo.pedido"),
            pagina.GetProperty("totalItens").GetInt32());
        var esperados = await IdsEmOrdemDoBancoAsync(0, 20);
        Assert.Equal(esperados, Ids(pagina));
    }

    private async Task<List<int>> IdsEmOrdemDoBancoAsync(int deslocamento, int quantidade)
    {
        await using var conexao = await banco.AbrirAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            $"SELECT id FROM dbo.pedido ORDER BY data_criacao DESC, id DESC OFFSET {deslocamento} ROWS FETCH NEXT {quantidade} ROWS ONLY";
        var ids = new List<int>();
        await using var leitor = await comando.ExecuteReaderAsync();
        while (await leitor.ReadAsync())
            ids.Add(leitor.GetInt32(0));
        return ids;
    }

    [Fact]
    public async Task RetornaNomesPrecosESubtotaisDosItensEDoCliente()
    {
        var cliente = await banco.InserirClienteAsync("Maria Silva");
        var teclado = await banco.InserirProdutoAsync(89.90m, estoque: 10, nome: "Teclado");
        var mouse = await banco.InserirProdutoAsync(79.90m, estoque: 10, nome: "Mouse");
        await banco.InserirPedidoAsync(cliente, Utc(2026, 11, 12, 14, 32, 10), (mouse, 1, 79.90m), (teclado, 2, 89.90m));

        var pagina = await ListarAsync($"?clienteId={cliente}");

        var pedido = Assert.Single(pagina.GetProperty("registros").EnumerateArray());
        Assert.Equal("Maria Silva", pedido.GetProperty("clienteNome").GetString());
        Assert.Equal("2026-11-12T14:32:10Z", pedido.GetProperty("dataCriacao").GetString());
        Assert.Equal(259.70m, pedido.GetProperty("valorTotal").GetDecimal());
        var itens = pedido.GetProperty("itens").EnumerateArray().ToList();
        Assert.Equal([Math.Min(teclado, mouse), Math.Max(teclado, mouse)], itens.Select(i => i.GetProperty("produtoId").GetInt32()));
        var itemTeclado = itens.Single(i => i.GetProperty("produtoId").GetInt32() == teclado);
        Assert.Equal("Teclado", itemTeclado.GetProperty("produtoNome").GetString());
        Assert.Equal(89.90m, itemTeclado.GetProperty("precoUnitario").GetDecimal());
        Assert.Equal(179.80m, itemTeclado.GetProperty("subtotal").GetDecimal());
    }

    [Trait("CA", "CA-19")]
    [Fact]
    public async Task FiltraPorClienteEClienteInexistenteRetornaListaVazia()
    {
        var a = await banco.InserirClienteAsync();
        var b = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(10m, estoque: 10);
        var pedidoA = await banco.InserirPedidoAsync(a, Utc(2026, 2, 1), (produto, 1, 10m));
        await banco.InserirPedidoAsync(b, Utc(2026, 2, 2), (produto, 1, 10m));

        var doCliente = await ListarAsync($"?clienteId={a}");
        var inexistente = await ListarAsync($"?clienteId={int.MaxValue}");

        Assert.Equal([pedidoA], Ids(doCliente));
        Assert.Empty(inexistente.GetProperty("registros").EnumerateArray());
        Assert.Equal(0, inexistente.GetProperty("totalItens").GetInt32());
    }

    [Trait("CA", "CA-20")]
    [Fact]
    public async Task IntervaloDeDatasEInclusivoNasDuasPontasEmUtc()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(10m, estoque: 10);
        var inicio = await banco.InserirPedidoAsync(cliente, Utc(2026, 11, 1), (produto, 1, 10m));
        var fim = await banco.InserirPedidoAsync(cliente, Utc(2026, 11, 12, 23, 59, 59), (produto, 1, 10m));
        var proximoDia = await banco.InserirPedidoAsync(cliente, Utc(2026, 11, 13), (produto, 1, 10m));
        var anterior = await banco.InserirPedidoAsync(cliente, Utc(2026, 10, 31, 23, 59, 59), (produto, 1, 10m));

        var pagina = await ListarAsync($"?clienteId={cliente}&dataInicio=2026-11-01&dataFim=2026-11-12");

        Assert.Equal([fim, inicio], Ids(pagina));
        Assert.DoesNotContain(proximoDia, Ids(pagina));
        Assert.DoesNotContain(anterior, Ids(pagina));
    }

    [Trait("CA", "CA-21")]
    [Fact]
    public async Task AceitaApenasUmaDasPontasComoIntervaloAberto()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(10m, estoque: 10);
        var antigo = await banco.InserirPedidoAsync(cliente, Utc(2026, 3, 1), (produto, 1, 10m));
        var meio = await banco.InserirPedidoAsync(cliente, Utc(2026, 3, 15), (produto, 1, 10m));
        var recente = await banco.InserirPedidoAsync(cliente, Utc(2026, 3, 30), (produto, 1, 10m));

        var apenasInicio = await ListarAsync($"?clienteId={cliente}&dataInicio=2026-03-15");
        var apenasFim = await ListarAsync($"?clienteId={cliente}&dataFim=2026-03-15");

        Assert.Equal([recente, meio], Ids(apenasInicio));
        Assert.Equal([meio, antigo], Ids(apenasFim));
    }

    [Trait("CA", "CA-22")]
    [Theory]
    [InlineData("?dataInicio=2026-11-13&dataFim=2026-11-12")]
    [InlineData("?dataInicio=2026-13-40")]
    [InlineData("?dataFim=12/11/2026")]
    [InlineData("?dataInicio=2026-1-5")]
    [InlineData("?pagina=0")]
    [InlineData("?pagina=abc")]
    [InlineData("?tamanhoPagina=0")]
    [InlineData("?tamanhoPagina=101")]
    [InlineData("?clienteId=abc")]
    [InlineData("?clienteId=1.5")]
    public async Task RetornaRequisicaoInvalidaParaParametrosIncorretos(string consulta)
    {
        var resposta = await GetAsync(consulta);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("REQUISICAO_INVALIDA", corpo.GetProperty("codigo").GetString());
        Assert.True(corpo.GetProperty("errors").EnumerateObject().Any());
    }

    [Fact]
    public async Task AgrupaOsParametrosInvalidosNaMesmaResposta()
    {
        var resposta = await GetAsync("?dataInicio=x&pagina=abc&clienteId=y");

        var erros = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(erros.TryGetProperty("dataInicio", out _));
        Assert.True(erros.TryGetProperty("pagina", out _));
        Assert.True(erros.TryGetProperty("clienteId", out _));
    }

    [Trait("CA", "CA-23")]
    [Fact]
    public async Task PaginarComMesmaDataDeCriacaoNaoRepeteNemOmiteRegistros()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(10m, estoque: 10);
        for (var i = 0; i < 8; i++)
            await banco.InserirPedidoAsync(cliente, Utc(2026, 4, 1, 12), (produto, 1, 10m));

        var completa = Ids(await ListarAsync($"?clienteId={cliente}&tamanhoPagina=100"));
        var concatenada = new List<int>();
        for (var pagina = 1; pagina <= 3; pagina++)
            concatenada.AddRange(Ids(await ListarAsync($"?clienteId={cliente}&tamanhoPagina=3&pagina={pagina}")));

        Assert.Equal(8, completa.Count);
        Assert.Equal(completa, concatenada);
        Assert.Equal(completa.Distinct().Count(), completa.Count);
        Assert.Equal(completa.OrderByDescending(id => id), completa);
        Assert.Equal(completa, Ids(await ListarAsync($"?clienteId={cliente}&tamanhoPagina=100")));
    }

    [Trait("CA", "CA-24")]
    [Fact]
    public async Task PaginaAlemDaUltimaRetornaListaVaziaPreservandoOsTotais()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(10m, estoque: 10);
        for (var i = 0; i < 5; i++)
            await banco.InserirPedidoAsync(cliente, Utc(2026, 5, 1).AddHours(i), (produto, 1, 10m));

        var pagina = await ListarAsync($"?clienteId={cliente}&tamanhoPagina=2&pagina=9");

        Assert.Empty(pagina.GetProperty("registros").EnumerateArray());
        Assert.Equal(5, pagina.GetProperty("totalItens").GetInt32());
        Assert.Equal(3, pagina.GetProperty("totalPaginas").GetInt32());
        Assert.Equal(9, pagina.GetProperty("pagina").GetInt32());
    }

    [Fact]
    public async Task PaginaMuitoGrandeNaoEstouraOOffset()
    {
        var pagina = await ListarAsync($"?pagina={int.MaxValue}&tamanhoPagina=100");

        Assert.Empty(pagina.GetProperty("registros").EnumerateArray());
    }

    [Trait("CA", "CA-25")]
    [Fact]
    public async Task FiltrosCombinadosAplicamAndETotalRefleteOFiltrado()
    {
        var a = await banco.InserirClienteAsync();
        var b = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(10m, estoque: 10);
        var alvo1 = await banco.InserirPedidoAsync(a, Utc(2026, 6, 10), (produto, 1, 10m));
        var alvo2 = await banco.InserirPedidoAsync(a, Utc(2026, 6, 20), (produto, 1, 10m));
        await banco.InserirPedidoAsync(a, Utc(2026, 7, 5), (produto, 1, 10m));
        await banco.InserirPedidoAsync(b, Utc(2026, 6, 15), (produto, 1, 10m));

        var pagina = await ListarAsync($"?clienteId={a}&dataInicio=2026-06-01&dataFim=2026-06-30&tamanhoPagina=1");

        Assert.Equal([alvo2], Ids(pagina));
        Assert.Equal(2, pagina.GetProperty("totalItens").GetInt32());
        Assert.Equal(2, pagina.GetProperty("totalPaginas").GetInt32());
        var segunda = await ListarAsync($"?clienteId={a}&dataInicio=2026-06-01&dataFim=2026-06-30&tamanhoPagina=1&pagina=2");
        Assert.Equal([alvo1], Ids(segunda));
    }
}
