using Pedidos.Domain;

namespace Pedidos.UnitTests.Domain;

public class PedidoCriarTests
{
    private static readonly DateTime Agora = new(2026, 11, 13, 10, 30, 15, DateTimeKind.Utc);

    private static ResultadoCriacaoPedido Criar(ItemSolicitado[] itens, params Produto[] produtos) =>
        Pedido.Criar(clienteId: 1, Agora, itens, produtos);

    private static ResultadoCriacaoPedido.Criado CriarComSucesso(ItemSolicitado[] itens, params Produto[] produtos) =>
        Assert.IsType<ResultadoCriacaoPedido.Criado>(Criar(itens, produtos));

    [Trait("CA", "CA-17")]
    [Fact]
    public void CalculaTotalExatoSemErroDePontoFlutuante()
    {
        var criado = CriarComSucesso([new(1, 3)], new Produto(1, "Item", 0.10m, 10));

        Assert.Equal(0.30m, criado.Pedido.ValorTotal);
    }

    [Trait("CA", "CA-10")]
    [Fact]
    public void CopiaPrecoDoCadastroParaOItem()
    {
        var criado = CriarComSucesso([new(1, 2)], new Produto(1, "Teclado", 89.90m, 10));

        var item = Assert.Single(criado.Pedido.Itens);
        Assert.Equal(89.90m, item.PrecoUnitario);
        Assert.Equal(179.80m, item.Subtotal);
    }

    [Fact]
    public void SomaOsSubtotaisNoValorTotal()
    {
        var criado = CriarComSucesso(
            [new(1, 2), new(2, 1)],
            new Produto(1, "Teclado", 89.90m, 10),
            new Produto(2, "Mouse", 79.90m, 10));

        Assert.Equal(259.70m, criado.Pedido.ValorTotal);
    }

    [Fact]
    public void GuardaClienteEDataTruncadaParaSegundos()
    {
        var comFracao = Agora.AddTicks(9_999_999);

        var resultado = Pedido.Criar(5, comFracao, [new(1, 1)], [new Produto(1, "Item", 10m, 1)]);

        var pedido = Assert.IsType<ResultadoCriacaoPedido.Criado>(resultado).Pedido;
        Assert.Equal(5, pedido.ClienteId);
        Assert.Equal(Agora, pedido.DataCriacao);
        Assert.Equal(DateTimeKind.Utc, pedido.DataCriacao.Kind);
    }

    [Fact]
    public void DevolveBaixasEmOrdemCrescenteDeProdutoSemAlterarOEstoque()
    {
        var produto9 = new Produto(9, "Nove", 10m, 5);
        var produto2 = new Produto(2, "Dois", 10m, 5);

        var criado = CriarComSucesso([new(9, 3), new(2, 1)], produto9, produto2);

        Assert.Equal([new BaixaEstoque(2, 1), new BaixaEstoque(9, 3)], criado.Baixas);
        Assert.Equal([2, 9], criado.Pedido.Itens.Select(i => i.ProdutoId));
        Assert.Equal(5, produto9.Estoque);
        Assert.Equal(5, produto2.Estoque);
    }

    [Trait("CA", "CA-07")]
    [Fact]
    public void AceitaQuantidadeIgualAoEstoque()
    {
        var criado = CriarComSucesso([new(1, 5)], new Produto(1, "Item", 10m, 5));

        Assert.Equal([new BaixaEstoque(1, 5)], criado.Baixas);
    }

    [Trait("CA", "CA-06")]
    [Fact]
    public void ListaTodosOsProdutosSemEstoqueComSolicitadoEDisponivel()
    {
        var resultado = Criar(
            [new(1, 6), new(2, 1), new(3, 2)],
            new Produto(1, "A", 10m, 5),
            new Produto(2, "B", 10m, 10),
            new Produto(3, "C", 10m, 0));

        var insuficiente = Assert.IsType<ResultadoCriacaoPedido.EstoqueInsuficiente>(resultado);
        Assert.Equal([new FaltaDeEstoque(1, 6, 5), new FaltaDeEstoque(3, 2, 0)], insuficiente.Produtos);
    }

    [Trait("CA", "CA-08")]
    [Fact]
    public void RejeitaQualquerQuantidadeParaProdutoSemEstoque()
    {
        var resultado = Criar([new(1, 1)], new Produto(1, "Item", 10m, 0));

        Assert.IsType<ResultadoCriacaoPedido.EstoqueInsuficiente>(resultado);
    }

    [Trait("CA", "CA-12")]
    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    public void UsaASomaDasLinhasRepetidasParaValidarEstoque(int estoque, bool aceito)
    {
        var consolidacao = ItemSolicitado.Consolidar([new(7, 2), new(7, 3)]);

        var resultado = Criar([.. consolidacao.Itens], new Produto(7, "Item", 10m, estoque));

        if (aceito)
        {
            var criado = Assert.IsType<ResultadoCriacaoPedido.Criado>(resultado);
            var item = Assert.Single(criado.Pedido.Itens);
            Assert.Equal(5, item.Quantidade);
        }
        else
        {
            var insuficiente = Assert.IsType<ResultadoCriacaoPedido.EstoqueInsuficiente>(resultado);
            Assert.Equal([new FaltaDeEstoque(7, 5, 4)], insuficiente.Produtos);
        }
    }

    [Trait("CA", "CA-05b")]
    [Fact]
    public void ListaTodosOsProdutosInexistentes()
    {
        var resultado = Criar([new(1, 1), new(4, 1), new(8, 1)], new Produto(1, "A", 10m, 5));

        var inexistentes = Assert.IsType<ResultadoCriacaoPedido.ProdutosInexistentes>(resultado);
        Assert.Equal([4, 8], inexistentes.ProdutoIds);
    }

    [Trait("CA", "CA-05c")]
    [Fact]
    public void ProdutoInexistenteTemPrioridadeSobreFaltaDeEstoque()
    {
        var resultado = Criar([new(1, 99), new(2, 1)], new Produto(1, "Sem saldo", 10m, 0));

        Assert.IsType<ResultadoCriacaoPedido.ProdutosInexistentes>(resultado);
    }

    [Trait("CA", "CA-17c")]
    [Fact]
    public void SubtotalAcimaDoLimiteMonetarioEMarcaOProduto()
    {
        var resultado = Criar(
            [new(1, 2), new(2, 1)],
            new Produto(1, "Caro", Limites.ValorMonetarioMaximo, 10),
            new Produto(2, "Barato", 1m, 10));

        var limite = Assert.IsType<ResultadoCriacaoPedido.LimiteExcedido>(resultado);
        Assert.Equal([1], limite.ProdutosComSubtotalExcedido);
        Assert.False(limite.TotalExcedido);
    }

    [Trait("CA", "CA-17c")]
    [Fact]
    public void TotalAcimaDoLimiteMonetarioComSubtotaisValidosMarcaOTotal()
    {
        var resultado = Criar(
            [new(1, 1), new(2, 1)],
            new Produto(1, "A", 6_000_000_000_000_000m, 10),
            new Produto(2, "B", 6_000_000_000_000_000m, 10));

        var limite = Assert.IsType<ResultadoCriacaoPedido.LimiteExcedido>(resultado);
        Assert.Empty(limite.ProdutosComSubtotalExcedido);
        Assert.True(limite.TotalExcedido);
    }

    [Trait("CA", "CA-17c")]
    [Fact]
    public void AceitaTotalExatamenteIgualAoLimiteMonetario()
    {
        var criado = CriarComSucesso([new(1, 1)], new Produto(1, "Limite", Limites.ValorMonetarioMaximo, 1));

        Assert.Equal(Limites.ValorMonetarioMaximo, criado.Pedido.ValorTotal);
    }

    [Trait("CA", "CA-17c")]
    [Fact]
    public void EstouroDaAritmeticaDecimalTambemEhLimiteExcedido()
    {
        // 2 bilhões x ~1e19 passa de 7,9e28 (máximo do decimal); o domínio não pode lançar exceção.
        var resultado = Criar([new(1, int.MaxValue)], new Produto(1, "Absurdo", 1e19m, int.MaxValue));

        var limite = Assert.IsType<ResultadoCriacaoPedido.LimiteExcedido>(resultado);
        Assert.Equal([1], limite.ProdutosComSubtotalExcedido);
    }

    [Trait("CA", "CA-17c")]
    [Fact]
    public void LimiteMonetarioTemPrioridadeSobreFaltaDeEstoque()
    {
        var resultado = Criar([new(1, 2)], new Produto(1, "Caro e sem saldo", Limites.ValorMonetarioMaximo, 0));

        Assert.IsType<ResultadoCriacaoPedido.LimiteExcedido>(resultado);
    }

    [Fact]
    public void RejeitaDataQueNaoEstaEmUtc()
    {
        var local = new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => Pedido.Criar(1, local, [new(1, 1)], [new Produto(1, "A", 1m, 1)]));
    }

    [Fact]
    public void RejeitaPedidoSemItens()
    {
        Assert.Throws<ArgumentException>(() => Pedido.Criar(1, Agora, [], [new Produto(1, "A", 1m, 1)]));
    }

    [Fact]
    public void RejeitaItensNaoConsolidados()
    {
        Assert.Throws<ArgumentException>(
            () => Pedido.Criar(1, Agora, [new(1, 1), new(1, 2)], [new Produto(1, "A", 1m, 10)]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejeitaClienteComIdNaoPositivo(int clienteId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Pedido.Criar(clienteId, Agora, [new(1, 1)], [new Produto(1, "A", 1m, 1)]));
    }
}
