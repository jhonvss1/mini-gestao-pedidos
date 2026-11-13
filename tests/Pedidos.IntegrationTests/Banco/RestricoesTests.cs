using Pedidos.IntegrationTests.Infra;

namespace Pedidos.IntegrationTests.Banco;

/// <summary>O banco rejeita gravações inválidas mesmo sem passar pela aplicação (última barreira).</summary>
public class RestricoesTests(BancoDeTeste banco) : IClassFixture<BancoDeTeste>, IAsyncLifetime
{
    private const int ViolacaoCheckOuFk = 547;
    private const int ViolacaoChaveUnica = 2627;

    public Task InitializeAsync() => banco.ExecutarScriptAsync("02-seed.sql");

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [Trait("CA", "CA-36")]
    [InlineData("UPDATE dbo.produto SET estoque = -1 WHERE id = 1", "ck_produto_estoque")]
    [InlineData("UPDATE dbo.produto SET preco = 0 WHERE id = 1", "ck_produto_preco")]
    [InlineData("UPDATE dbo.produto SET preco = -5 WHERE id = 1", "ck_produto_preco")]
    [InlineData("INSERT INTO dbo.item_pedido (pedido_id, produto_id, quantidade, preco_unitario) VALUES (11, 7, 0, 10)",
        "ck_item_pedido_quantidade")]
    [InlineData("INSERT INTO dbo.item_pedido (pedido_id, produto_id, quantidade, preco_unitario) VALUES (11, 7, -2, 10)",
        "ck_item_pedido_quantidade")]
    [InlineData("INSERT INTO dbo.item_pedido (pedido_id, produto_id, quantidade, preco_unitario) VALUES (11, 7, 1, 0)",
        "ck_item_pedido_preco_unitario")]
    public async Task CheckRejeitaValorInvalido(string sql, string restricao)
    {
        var erro = await banco.ErroDoComandoAsync(sql);

        Assert.NotNull(erro);
        Assert.Equal(ViolacaoCheckOuFk, erro.Number);
        Assert.Contains(restricao, erro.Message);
    }

    [Fact]
    [Trait("CA", "CA-36")]
    public async Task RejeitaItemDuplicadoNoPedido()
    {
        // O pedido 1 já tem o produto 3 na carga inicial.
        var erro = await banco.ErroDoComandoAsync(
            "INSERT INTO dbo.item_pedido (pedido_id, produto_id, quantidade, preco_unitario) VALUES (1, 3, 1, 10)");

        Assert.NotNull(erro);
        Assert.Equal(ViolacaoChaveUnica, erro.Number);
        Assert.Contains("uq_item_pedido_pedido_id_produto_id", erro.Message);
    }

    [Fact]
    [Trait("CA", "CA-36")]
    public async Task AceitaGravacaoValida()
    {
        // Controle: sem a violação, o mesmo tipo de comando funciona (as rejeições acima vêm das restrições).
        // Usa o pedido 12 para não colidir com o par (11, 7) das inserções inválidas, que independem da ordem.
        var erro = await banco.ErroDoComandoAsync(
            "INSERT INTO dbo.item_pedido (pedido_id, produto_id, quantidade, preco_unitario) VALUES (12, 7, 1, 10)");

        Assert.Null(erro);
    }
}
