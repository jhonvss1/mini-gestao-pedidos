using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Pedidos.IntegrationTests.Suporte;

namespace Pedidos.IntegrationTests.Api;

public class CriarPedidoApiTests(BancoDeTeste banco) : IClassFixture<BancoDeTeste>, IDisposable
{
    private readonly FabricaApi _fabrica = new(banco.StringConexao);

    public void Dispose() => _fabrica.Dispose();

    private async Task<HttpResponseMessage> PostarAsync(string json) =>
        await _fabrica.CreateClient().PostAsync("/pedidos", new StringContent(json, Encoding.UTF8, "application/json"));

    private Task<HttpResponseMessage> PostarAsync(int? clienteId, params (int? ProdutoId, int? Quantidade)[] itens)
    {
        var corpo = new
        {
            clienteId,
            itens = itens.Select(i => new { produtoId = i.ProdutoId, quantidade = i.Quantidade }),
        };
        return _fabrica.CreateClient().PostAsJsonAsync("/pedidos", corpo);
    }

    private static async Task<JsonElement> LerAsync(HttpResponseMessage resposta) =>
        await resposta.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<int> ContarPedidosAsync(int clienteId) =>
        await banco.EscalarAsync<int>($"SELECT COUNT(*) FROM dbo.pedido WHERE cliente_id = {clienteId}");

    [Trait("CA", "CA-01")]
    [Fact]
    public async Task CriaPedidoEDebitaOEstoque()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(89.90m, estoque: 10);

        var resposta = await PostarAsync(cliente, (produto, 2));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Null(resposta.Headers.Location);
        var corpo = await LerAsync(resposta);
        Assert.True(corpo.GetProperty("id").GetInt32() > 0);
        Assert.Equal(cliente, corpo.GetProperty("clienteId").GetInt32());
        Assert.Equal(179.80m, corpo.GetProperty("valorTotal").GetDecimal());
        var item = Assert.Single(corpo.GetProperty("itens").EnumerateArray());
        Assert.Equal(produto, item.GetProperty("produtoId").GetInt32());
        Assert.Equal(2, item.GetProperty("quantidade").GetInt32());
        Assert.Equal(89.90m, item.GetProperty("precoUnitario").GetDecimal());
        Assert.Equal(179.80m, item.GetProperty("subtotal").GetDecimal());
        Assert.Equal(8, await banco.EstoqueAsync(produto));
    }

    [Fact]
    public async Task RetornaADataDeCriacaoEmUtcTruncadaParaSegundos()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(10m, estoque: 10);

        var resposta = await PostarAsync(cliente, (produto, 1));

        var corpo = await LerAsync(resposta);
        Assert.Equal("2026-11-13T10:30:15Z", corpo.GetProperty("dataCriacao").GetString());
        Assert.Equal(
            new DateTime(2026, 11, 13, 10, 30, 15),
            await banco.EscalarAsync<DateTime>($"SELECT data_criacao FROM dbo.pedido WHERE id = {corpo.GetProperty("id").GetInt32()}"));
    }

    [Trait("CA", "CA-02")]
    [Theory]
    [InlineData("{\"clienteId\":1,\"itens\":[]}")]
    [InlineData("{\"clienteId\":1}")]
    [InlineData("{\"clienteId\":1,\"itens\":null}")]
    public async Task RetornaValidacaoParaItensVaziosOuAusentes(string json)
    {
        var resposta = await PostarAsync(json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        var corpo = await LerAsync(resposta);
        Assert.Equal("VALIDACAO", corpo.GetProperty("codigo").GetString());
        Assert.True(corpo.GetProperty("errors").TryGetProperty("itens", out _));
    }

    [Trait("CA", "CA-03")]
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(null)]
    public async Task RetornaValidacaoParaQuantidadeNaoPositivaOuAusente(int? quantidade)
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(10m, estoque: 10);

        var resposta = await PostarAsync(cliente, (produto, quantidade));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        var corpo = await LerAsync(resposta);
        Assert.Equal("VALIDACAO", corpo.GetProperty("codigo").GetString());
        Assert.True(corpo.GetProperty("errors").TryGetProperty("itens[0].quantidade", out _));
        Assert.Equal(10, await banco.EstoqueAsync(produto));
        Assert.Equal(0, await ContarPedidosAsync(cliente));
    }

    [Trait("CA", "CA-03")]
    [Theory]
    [InlineData("{\"clienteId\":1,\"itens\":[{\"produtoId\":1,\"quantidade\":1.5}]}")]
    [InlineData("{\"clienteId\":1,\"itens\":[{\"produtoId\":1,\"quantidade\":\"abc\"}]}")]
    [InlineData("{\"clienteId\":\"texto\",\"itens\":[{\"produtoId\":1,\"quantidade\":1}]}")]
    [InlineData("{ json inválido")]
    [InlineData("")]
    public async Task RetornaRequisicaoInvalidaParaCorpoMalformadoOuTiposErrados(string json)
    {
        var resposta = await PostarAsync(json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var corpo = await LerAsync(resposta);
        Assert.Equal("REQUISICAO_INVALIDA", corpo.GetProperty("codigo").GetString());
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("System.", corpo.ToString());
    }

    [Trait("CA", "CA-04")]
    [Fact]
    public async Task RetornaNaoEncontradoParaClienteInexistente()
    {
        var produto = await banco.InserirProdutoAsync(10m, estoque: 10);

        var resposta = await PostarAsync(int.MaxValue, (produto, 1));

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("CLIENTE_NAO_ENCONTRADO", (await LerAsync(resposta)).GetProperty("codigo").GetString());
        Assert.Equal(10, await banco.EstoqueAsync(produto));
    }

    [Trait("CA", "CA-05")]
    [Trait("CA", "CA-05b")]
    [Fact]
    public async Task ListaTodosOsProdutosInexistentesSemAlterarOsValidos()
    {
        var cliente = await banco.InserirClienteAsync();
        var valido = await banco.InserirProdutoAsync(10m, estoque: 10);

        var resposta = await PostarAsync(cliente, (valido, 1), (int.MaxValue, 1), (int.MaxValue - 1, 2));

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        var corpo = await LerAsync(resposta);
        Assert.Equal("PRODUTO_NAO_ENCONTRADO", corpo.GetProperty("codigo").GetString());
        var produtos = corpo.GetProperty("produtos").EnumerateArray().ToList();
        Assert.Equal(2, produtos.Count);
        Assert.Equal([int.MaxValue - 1, int.MaxValue], produtos.Select(p => p.GetProperty("produtoId").GetInt32()));
        Assert.Contains("itens[1]", produtos[1].GetProperty("mensagem").GetString());
        Assert.Equal(10, await banco.EstoqueAsync(valido));
        Assert.Equal(0, await ContarPedidosAsync(cliente));
    }

    [Trait("CA", "CA-05c")]
    [Fact]
    public async Task RespeitaAOrdemDasEtapasDeValidacao()
    {
        var cliente = await banco.InserirClienteAsync();
        var semEstoque = await banco.InserirProdutoAsync(10m, estoque: 0);

        var clienteInexistente = await PostarAsync(int.MaxValue, (semEstoque, 1));
        var produtoInexistente = await PostarAsync(cliente, (semEstoque, 1), (int.MaxValue, 1));
        var estruturaInvalida = await PostarAsync(int.MaxValue, (semEstoque, 0));

        Assert.Equal(HttpStatusCode.NotFound, clienteInexistente.StatusCode);
        Assert.Equal("CLIENTE_NAO_ENCONTRADO", (await LerAsync(clienteInexistente)).GetProperty("codigo").GetString());
        Assert.Equal(HttpStatusCode.NotFound, produtoInexistente.StatusCode);
        Assert.Equal("PRODUTO_NAO_ENCONTRADO", (await LerAsync(produtoInexistente)).GetProperty("codigo").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, estruturaInvalida.StatusCode);
    }

    [Trait("CA", "CA-05d")]
    [Fact]
    public async Task AgrupaOsErrosDeValidacaoDeVariosItens()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(10m, estoque: 10);

        var resposta = await PostarAsync(cliente, (produto, 0), (produto, 1), (produto, -3));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        var erros = (await LerAsync(resposta)).GetProperty("errors");
        Assert.True(erros.TryGetProperty("itens[0].quantidade", out _));
        Assert.True(erros.TryGetProperty("itens[2].quantidade", out _));
        Assert.False(erros.TryGetProperty("itens[1].quantidade", out _));
    }

    [Trait("CA", "CA-06")]
    [Fact]
    public async Task RetornaConflitoComSolicitadoEDisponivelDeTodosOsProdutosAfetados()
    {
        var cliente = await banco.InserirClienteAsync();
        var a = await banco.InserirProdutoAsync(10m, estoque: 3);
        var b = await banco.InserirProdutoAsync(10m, estoque: 0);
        var c = await banco.InserirProdutoAsync(10m, estoque: 50);

        var resposta = await PostarAsync(cliente, (a, 5), (b, 1), (c, 2));

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        var corpo = await LerAsync(resposta);
        Assert.Equal("ESTOQUE_INSUFICIENTE", corpo.GetProperty("codigo").GetString());
        var produtos = corpo.GetProperty("produtos").EnumerateArray().ToList();
        Assert.Equal(2, produtos.Count);
        Assert.Equal(a, produtos[0].GetProperty("produtoId").GetInt32());
        Assert.Equal(5, produtos[0].GetProperty("solicitado").GetInt32());
        Assert.Equal(3, produtos[0].GetProperty("disponivel").GetInt32());
        Assert.Equal(50, await banco.EstoqueAsync(c));
        Assert.Equal(0, await ContarPedidosAsync(cliente));
    }

    [Trait("CA", "CA-07")]
    [Fact]
    public async Task AceitaQuantidadeIgualAoEstoqueEZeraOSaldo()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(10m, estoque: 5);

        var resposta = await PostarAsync(cliente, (produto, 5));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Equal(0, await banco.EstoqueAsync(produto));
    }

    [Trait("CA", "CA-08")]
    [Fact]
    public async Task RejeitaQualquerQuantidadeParaProdutoSemEstoque()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(10m, estoque: 0);

        var resposta = await PostarAsync(cliente, (produto, 1));

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Trait("CA", "CA-09")]
    [Fact]
    public async Task SegundoItemSemEstoqueMantemOEstoqueDoPrimeiroIntacto()
    {
        var cliente = await banco.InserirClienteAsync();
        var primeiro = await banco.InserirProdutoAsync(10m, estoque: 10);
        var segundo = await banco.InserirProdutoAsync(10m, estoque: 0);

        var resposta = await PostarAsync(cliente, (primeiro, 2), (segundo, 1));

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        Assert.Equal(10, await banco.EstoqueAsync(primeiro));
        Assert.Equal(0, await ContarPedidosAsync(cliente));
    }

    [Trait("CA", "CA-10")]
    [Fact]
    public async Task MantemPrecoEValorTotalMesmoSeOProdutoMudarDePrecoDepois()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(89.90m, estoque: 10);
        var corpo = await LerAsync(await PostarAsync(cliente, (produto, 2)));
        var pedidoId = corpo.GetProperty("id").GetInt32();

        await banco.ErroDoComandoAsync($"UPDATE dbo.produto SET preco = 99.90 WHERE id = {produto}");

        Assert.Equal(89.90m, await banco.EscalarAsync<decimal>(
            $"SELECT preco_unitario FROM dbo.item_pedido WHERE pedido_id = {pedidoId}"));
        Assert.Equal(179.80m, await banco.EscalarAsync<decimal>(
            $"SELECT valor_total FROM dbo.pedido WHERE id = {pedidoId}"));
    }

    [Trait("CA", "CA-11")]
    [Fact]
    public async Task IgnoraPrecoUnitarioEnviadoNoCorpo()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(89.90m, estoque: 10);
        var json = $$"""{"clienteId":{{cliente}},"itens":[{"produtoId":{{produto}},"quantidade":1,"precoUnitario":0.01}]}""";

        var resposta = await PostarAsync(json);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Equal(89.90m, (await LerAsync(resposta)).GetProperty("valorTotal").GetDecimal());
    }

    [Trait("CA", "CA-12")]
    [Fact]
    public async Task LinhasRepetidasSaoConsolidadasEUsamASomaParaValidarEstoque()
    {
        var cliente = await banco.InserirClienteAsync();
        var estoque4 = await banco.InserirProdutoAsync(10m, estoque: 4);
        var estoque5 = await banco.InserirProdutoAsync(10m, estoque: 5);

        var rejeitado = await PostarAsync(cliente, (estoque4, 2), (estoque4, 3));
        var aceito = await PostarAsync(cliente, (estoque5, 2), (estoque5, 3));

        Assert.Equal(HttpStatusCode.Conflict, rejeitado.StatusCode);
        Assert.Equal(4, await banco.EstoqueAsync(estoque4));
        Assert.Equal(HttpStatusCode.Created, aceito.StatusCode);
        var item = Assert.Single((await LerAsync(aceito)).GetProperty("itens").EnumerateArray());
        Assert.Equal(5, item.GetProperty("quantidade").GetInt32());
        Assert.Equal(0, await banco.EstoqueAsync(estoque5));
    }

    [Trait("CA", "CA-17")]
    [Fact]
    public async Task CalculaTotalExatoSemErroDePontoFlutuante()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(0.10m, estoque: 10);

        var resposta = await PostarAsync(cliente, (produto, 3));

        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.Contains("\"valorTotal\":0.30", corpo);
        Assert.DoesNotContain("0.30000000000000004", corpo);
        Assert.Equal(0.30m, await banco.EscalarAsync<decimal>(
            $"SELECT valor_total FROM dbo.pedido WHERE cliente_id = {cliente}"));
    }

    [Trait("CA", "CA-17b")]
    [Fact]
    public async Task RetornaValidacaoQuandoASomaDasQuantidadesExcedeInt()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(1m, estoque: 10);

        var resposta = await PostarAsync(cliente, (produto, int.MaxValue), (produto, 1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Equal("VALIDACAO", (await LerAsync(resposta)).GetProperty("codigo").GetString());
        Assert.Equal(10, await banco.EstoqueAsync(produto));
        Assert.Equal(0, await ContarPedidosAsync(cliente));
    }

    [Trait("CA", "CA-17c")]
    [Fact]
    public async Task RetornaValidacaoQuandoOSubtotalExcedeAPrecisaoMonetaria()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(9_999_999_999_999_999.99m, estoque: 10);

        var resposta = await PostarAsync(cliente, (produto, 2));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Equal("VALIDACAO", (await LerAsync(resposta)).GetProperty("codigo").GetString());
        Assert.Equal(10, await banco.EstoqueAsync(produto));
        Assert.Equal(0, await ContarPedidosAsync(cliente));
    }
}
