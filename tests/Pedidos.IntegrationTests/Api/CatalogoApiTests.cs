using System.Net.Http.Json;
using System.Text.Json;
using Pedidos.IntegrationTests.Infra;

namespace Pedidos.IntegrationTests.Api;

public class CatalogoApiTests(BancoDeTeste banco) : IClassFixture<BancoDeTeste>, IDisposable
{
    private readonly FabricaApi _fabrica = new(banco.StringConexao);

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public async Task ListaClientesOrdenadosPorNomeEId()
    {
        var segundoA = await banco.InserirClienteAsync("ZZ-Catalogo A");
        var b = await banco.InserirClienteAsync("ZZ-Catalogo B");
        var primeiroA = await banco.InserirClienteAsync("ZZ-Catalogo A");
        // Ordem de inserção: A (id menor), B, A (id maior). Esperado: A, A, B.
        var esperado = new[] { segundoA, primeiroA, b };

        var clientes = (await _fabrica.CreateClient().GetFromJsonAsync<JsonElement>("/clientes"))
            .EnumerateArray()
            .Where(c => c.GetProperty("nome").GetString()!.StartsWith("ZZ-Catalogo"))
            .ToList();

        Assert.Equal(esperado, clientes.Select(c => c.GetProperty("id").GetInt32()));
        Assert.Equal("ZZ-Catalogo A", clientes[0].GetProperty("nome").GetString());
    }

    [Fact]
    public async Task ListaProdutosComPrecoEEstoqueOrdenadosPorNomeEId()
    {
        var mouse = await banco.InserirProdutoAsync(79.90m, estoque: 12, nome: "ZZ-Produto Mouse");
        var teclado = await banco.InserirProdutoAsync(89.90m, estoque: 0, nome: "ZZ-Produto Teclado");

        var produtos = (await _fabrica.CreateClient().GetFromJsonAsync<JsonElement>("/produtos"))
            .EnumerateArray()
            .Where(p => p.GetProperty("nome").GetString()!.StartsWith("ZZ-Produto"))
            .ToList();

        Assert.Equal([mouse, teclado], produtos.Select(p => p.GetProperty("id").GetInt32()));
        Assert.Equal(79.90m, produtos[0].GetProperty("preco").GetDecimal());
        Assert.Equal(12, produtos[0].GetProperty("estoque").GetInt32());
        Assert.Equal(0, produtos[1].GetProperty("estoque").GetInt32());
    }
}
