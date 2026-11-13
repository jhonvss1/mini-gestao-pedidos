using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Pedidos.IntegrationTests.Suporte;

namespace Pedidos.IntegrationTests.Banco;

/// <summary>
/// O login da API em produção (03 e 04) só pode o que a aplicação usa: nada de apagar, mudar preço ou alterar o schema.
/// Além de negar o que não deve, o teste prova que o mínimo concedido basta para o fluxo real de criar e listar pedidos.
/// </summary>
public class PermissoesDaAplicacaoTests(BancoDeTeste banco) : IClassFixture<BancoDeTeste>, IAsyncLifetime
{
    private const string Usuario = "app_pedidos";
    private const string Senha = "Chave_Teste_Banco_2026!";

    public async Task InitializeAsync()
    {
        await ConfigurarAsync(Senha);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task ConfigurarAsync(string senha)
    {
        await banco.ExecutarScriptNoServidorAsync("03-login-aplicacao.sql", new Dictionary<string, string> { ["SENHA_APP"] = senha });
        await banco.ExecutarScriptAsync("04-permissoes-aplicacao.sql");
    }

    private async Task<SqlException?> ComoAplicacaoAsync(string sql)
    {
        try
        {
            await using var conexao = new SqlConnection(banco.StringConexaoComo(Usuario, Senha));
            await conexao.OpenAsync();
            await using var comando = new SqlCommand(sql, conexao);
            await comando.ExecuteNonQueryAsync();
            return null;
        }
        catch (SqlException ex)
        {
            return ex;
        }
    }

    [Fact]
    public async Task CriaEListaPedidosComOMinimoDePermissao()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(89.90m, estoque: 10);
        using var fabrica = new FabricaApi(banco.StringConexaoComo(Usuario, Senha));
        var http = fabrica.CreateClient();

        var criacao = await http.PostAsJsonAsync(
            "/api/pedidos",
            new { clienteId = cliente, itens = new[] { new { produtoId = produto, quantidade = 3 } } });
        var listagem = await http.GetAsync($"/api/pedidos?clienteId={cliente}");
        var saude = await http.GetAsync("/api/saude");

        Assert.Equal(HttpStatusCode.Created, criacao.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listagem.StatusCode);
        Assert.Equal(HttpStatusCode.OK, saude.StatusCode);
        Assert.Equal(7, await banco.EstoqueAsync(produto));
    }

    [Theory]
    [InlineData("DELETE FROM dbo.pedido")]
    [InlineData("DELETE FROM dbo.item_pedido")]
    [InlineData("DELETE FROM dbo.cliente")]
    [InlineData("UPDATE dbo.produto SET preco = 1")]
    [InlineData("UPDATE dbo.produto SET nome = N'x'")]
    [InlineData("UPDATE dbo.pedido SET valor_total = 1")]
    [InlineData("INSERT INTO dbo.cliente (nome) VALUES (N'x')")]
    [InlineData("INSERT INTO dbo.produto (nome, preco, estoque) VALUES (N'x', 1, 1)")]
    [InlineData("DROP TABLE dbo.item_pedido")]
    [InlineData("ALTER TABLE dbo.produto ADD teste int NULL")]
    public async Task NegaOQueAAplicacaoNaoPrecisaFazer(string sql)
    {
        var erro = await ComoAplicacaoAsync(sql);

        Assert.NotNull(erro);
        // 229 e 230: permissão negada em objeto/coluna; 262, 1088 e 3701: sem permissão para alterar ou apagar objetos.
        Assert.Contains(erro.Number, new[] { 229, 230, 262, 1088, 3701 });
    }

    [Fact]
    public async Task PermiteBaixarOEstoqueMasSoDessaColuna()
    {
        var produto = await banco.InserirProdutoAsync(10m, estoque: 5);

        var baixa = await ComoAplicacaoAsync($"UPDATE dbo.produto SET estoque = estoque - 1 WHERE id = {produto} AND estoque >= 1");

        Assert.Null(baixa);
        Assert.Equal(4, await banco.EstoqueAsync(produto));
    }

    [Fact]
    public async Task ReexecutarOsScriptsNaoFalhaEAtualizaASenha()
    {
        const string novaSenha = "Outra_Chave_Teste_2026!";

        await ConfigurarAsync(Senha); // segunda execução com a mesma senha: idempotente
        await ConfigurarAsync(novaSenha); // rotação de segredo

        await using var conexao = new SqlConnection(banco.StringConexaoComo(Usuario, novaSenha));
        await conexao.OpenAsync();
        Assert.Equal(System.Data.ConnectionState.Open, conexao.State);
        await ConfigurarAsync(Senha); // volta a senha original para os demais testes da classe
    }
}
