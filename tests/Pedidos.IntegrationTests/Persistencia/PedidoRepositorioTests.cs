using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pedidos.Domain;
using Pedidos.Infrastructure.Persistencia;
using Pedidos.IntegrationTests.Infra;

namespace Pedidos.IntegrationTests.Persistencia;

public class PedidoRepositorioTests(BancoDeTeste banco) : IClassFixture<BancoDeTeste>
{
    private static readonly DateTime Agora = new(2026, 11, 13, 10, 30, 15, DateTimeKind.Utc);

    private PedidoRepositorio CriarRepositorio(params IInterceptor[] interceptors)
    {
        var opcoes = new DbContextOptionsBuilder<PedidosDbContext>()
            .UseSqlServer(banco.StringConexao)
            .AddInterceptors(interceptors)
            .Options;
        return new PedidoRepositorio(new PedidosDbContext(opcoes));
    }

    private static Func<IReadOnlyCollection<Produto>, ResultadoCriacaoPedido> Decidir(
        int clienteId,
        params ItemSolicitado[] itens) =>
        produtos => Pedido.Criar(clienteId, Agora, itens, produtos);

    private async Task<int> ContarPedidosAsync(int clienteId) =>
        await banco.EscalarAsync<int>($"SELECT COUNT(*) FROM dbo.pedido WHERE cliente_id = {clienteId}");

    [Fact]
    public async Task GravaPedidoEItensComPrecoDoCadastroEDebitaOEstoque()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(89.90m, estoque: 10);
        ItemSolicitado[] itens = [new(produto, 3)];

        var resultado = await CriarRepositorio().CriarAsync(cliente, itens, Decidir(cliente, itens), default);

        var criado = Assert.IsType<ResultadoCriacaoPedido.Criado>(resultado);
        Assert.True(criado.Pedido.Id > 0);
        Assert.Equal(7, await banco.EstoqueAsync(produto));
        Assert.Equal(269.70m, await banco.EscalarAsync<decimal>(
            $"SELECT valor_total FROM dbo.pedido WHERE id = {criado.Pedido.Id}"));
        Assert.Equal(89.90m, await banco.EscalarAsync<decimal>(
            $"SELECT preco_unitario FROM dbo.item_pedido WHERE pedido_id = {criado.Pedido.Id}"));
    }

    [Trait("CA", "CA-07")]
    [Fact]
    public async Task AceitaQuantidadeIgualAoEstoqueEZeraOSaldo()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(10m, estoque: 5);
        ItemSolicitado[] itens = [new(produto, 5)];

        var resultado = await CriarRepositorio().CriarAsync(cliente, itens, Decidir(cliente, itens), default);

        Assert.IsType<ResultadoCriacaoPedido.Criado>(resultado);
        Assert.Equal(0, await banco.EstoqueAsync(produto));
    }

    [Trait("CA", "CA-06")]
    [Fact]
    public async Task NaoGravaNadaQuandoOEstoqueEInsuficiente()
    {
        var cliente = await banco.InserirClienteAsync();
        var produto = await banco.InserirProdutoAsync(10m, estoque: 3);
        ItemSolicitado[] itens = [new(produto, 5)];

        var resultado = await CriarRepositorio().CriarAsync(cliente, itens, Decidir(cliente, itens), default);

        var insuficiente = Assert.IsType<ResultadoCriacaoPedido.EstoqueInsuficiente>(resultado);
        Assert.Equal([new FaltaDeEstoque(produto, 5, 3)], insuficiente.Produtos);
        Assert.Equal(3, await banco.EstoqueAsync(produto));
        Assert.Equal(0, await ContarPedidosAsync(cliente));
    }

    [Trait("CA", "CA-09")]
    [Fact]
    public async Task SegundoProdutoSemEstoqueMantemOEstoqueDoPrimeiroIntacto()
    {
        var cliente = await banco.InserirClienteAsync();
        var primeiro = await banco.InserirProdutoAsync(10m, estoque: 10);
        var segundo = await banco.InserirProdutoAsync(10m, estoque: 0);
        ItemSolicitado[] itens = [new(primeiro, 2), new(segundo, 1)];

        var resultado = await CriarRepositorio().CriarAsync(cliente, itens, Decidir(cliente, itens), default);

        Assert.IsType<ResultadoCriacaoPedido.EstoqueInsuficiente>(resultado);
        Assert.Equal(10, await banco.EstoqueAsync(primeiro));
        Assert.Equal(0, await ContarPedidosAsync(cliente));
    }

    [Trait("CA", "CA-13")]
    [Fact]
    public async Task FalhaAposOsDebitosReverteEstoquePedidoEItens()
    {
        var cliente = await banco.InserirClienteAsync();
        var primeiro = await banco.InserirProdutoAsync(10m, estoque: 10);
        var segundo = await banco.InserirProdutoAsync(10m, estoque: 10);
        ItemSolicitado[] itens = [new(primeiro, 2), new(segundo, 3)];
        var repositorio = CriarRepositorio(new FalhaAoSalvarInterceptor());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repositorio.CriarAsync(cliente, itens, Decidir(cliente, itens), default));

        Assert.Equal(10, await banco.EstoqueAsync(primeiro));
        Assert.Equal(10, await banco.EstoqueAsync(segundo));
        Assert.Equal(0, await ContarPedidosAsync(cliente));
    }

    [Trait("CA", "CA-15")]
    [Fact]
    public async Task PerderADisputaDesfazOsDebitosAnterioresEReavaliaComOSaldoReal()
    {
        var cliente = await banco.InserirClienteAsync();
        var primeiro = await banco.InserirProdutoAsync(10m, estoque: 10);
        var segundo = await banco.InserirProdutoAsync(10m, estoque: 10);
        ItemSolicitado[] itens = [new(primeiro, 2), new(segundo, 6)];

        var chamadas = 0;
        ResultadoCriacaoPedido DecidirComConcorrente(IReadOnlyCollection<Produto> produtos)
        {
            // Outra requisição consome o estoque do segundo produto depois do snapshot e antes do UPDATE.
            if (++chamadas == 1)
                banco.ErroDoComandoAsync($"UPDATE dbo.produto SET estoque = 4 WHERE id = {segundo}").GetAwaiter().GetResult();

            return Pedido.Criar(cliente, Agora, itens, produtos);
        }

        var resultado = await CriarRepositorio().CriarAsync(cliente, itens, DecidirComConcorrente, default);

        var insuficiente = Assert.IsType<ResultadoCriacaoPedido.EstoqueInsuficiente>(resultado);
        Assert.Equal([new FaltaDeEstoque(segundo, 6, 4)], insuficiente.Produtos);
        Assert.Equal(2, chamadas);
        Assert.Equal(10, await banco.EstoqueAsync(primeiro));
        Assert.Equal(4, await banco.EstoqueAsync(segundo));
        Assert.Equal(0, await ContarPedidosAsync(cliente));
    }

    [Fact]
    public async Task NaoDebitaNemGravaQuandoProdutoNaoExiste()
    {
        var cliente = await banco.InserirClienteAsync();
        var existente = await banco.InserirProdutoAsync(10m, estoque: 10);
        ItemSolicitado[] itens = [new(existente, 1), new(int.MaxValue, 1)];

        var resultado = await CriarRepositorio().CriarAsync(cliente, itens, Decidir(cliente, itens), default);

        var inexistentes = Assert.IsType<ResultadoCriacaoPedido.ProdutosInexistentes>(resultado);
        Assert.Equal([int.MaxValue], inexistentes.ProdutoIds);
        Assert.Equal(10, await banco.EstoqueAsync(existente));
    }

    [Fact]
    public async Task InformaSeOClienteExiste()
    {
        var cliente = await banco.InserirClienteAsync();
        var repositorio = CriarRepositorio();

        Assert.True(await repositorio.ClienteExisteAsync(cliente, default));
        Assert.False(await repositorio.ClienteExisteAsync(int.MaxValue, default));
    }

    /// <summary>Simula uma falha na gravação do pedido, depois dos débitos e antes do commit.</summary>
    private sealed class FalhaAoSalvarInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Falha injetada na gravação.");
    }
}
