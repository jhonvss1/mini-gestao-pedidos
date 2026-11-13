using Pedidos.Application;
using Pedidos.Domain;

namespace Pedidos.UnitTests.Application;

public class ValidadorCriarPedidoTests
{
    private static CriarPedidoComando Comando(int? clienteId, params ItemComando?[] itens) =>
        new CriarPedidoComando(clienteId, itens);

    [Fact]
    public void AceitaComandoValidoEConsolidaOsItens()
    {
        var resultado = ValidadorCriarPedido.Validar(Comando(3, new ItemComando(7, 2), new ItemComando(9, 1), new ItemComando(7, 3)));

        Assert.True(resultado.Valido);
        Assert.Equal([new ItemSolicitado(7, 5), new ItemSolicitado(9, 1)], resultado.Itens);
    }

    [Trait("CA", "CA-05d")]
    [Fact]
    public void AgrupaOsErrosDeQuantidadeDeTodosOsItensInvalidos()
    {
        var resultado = ValidadorCriarPedido.Validar(Comando(3, new ItemComando(7, 0), new ItemComando(9, 1), new ItemComando(8, -2)));

        Assert.Equal(["itens[0].quantidade", "itens[2].quantidade"], resultado.Erros.Select(e => e.Campo));
        Assert.All(resultado.Erros, e => Assert.Equal(CodigosErro.Validacao, e.Codigo));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-4)]
    public void RejeitaClienteIdAusenteOuNaoPositivo(int? clienteId)
    {
        var resultado = ValidadorCriarPedido.Validar(Comando(clienteId, new ItemComando(7, 1)));

        var erro = Assert.Single(resultado.Erros);
        Assert.Equal("clienteId", erro.Campo);
    }

    [Fact]
    public void RejeitaItensAusentes()
    {
        var resultado = ValidadorCriarPedido.Validar(new CriarPedidoComando(3, null));

        Assert.Equal("itens", Assert.Single(resultado.Erros).Campo);
    }

    [Fact]
    public void RejeitaItensVazios()
    {
        var resultado = ValidadorCriarPedido.Validar(Comando(3));

        Assert.Equal("itens", Assert.Single(resultado.Erros).Campo);
    }

    [Fact]
    public void RejeitaProdutoIdEQuantidadeAusentesNoMesmoItem()
    {
        var resultado = ValidadorCriarPedido.Validar(Comando(3, new ItemComando(null, null)));

        Assert.Equal(["itens[0].produtoId", "itens[0].quantidade"], resultado.Erros.Select(e => e.Campo));
    }

    [Fact]
    public void RejeitaItemNulo()
    {
        var resultado = ValidadorCriarPedido.Validar(Comando(3, new ItemComando(7, 1), null));

        Assert.Equal("itens[1]", Assert.Single(resultado.Erros).Campo);
    }

    [Fact]
    public void AcumulaErrosDeClienteEItensNaMesmaEtapa()
    {
        var resultado = ValidadorCriarPedido.Validar(Comando(0, new ItemComando(7, 0)));

        Assert.Equal(["clienteId", "itens[0].quantidade"], resultado.Erros.Select(e => e.Campo));
    }

    [Trait("CA", "CA-17b")]
    [Fact]
    public void RejeitaSomaConsolidadaAcimaDeIntNasDuasLinhasDoProduto()
    {
        var resultado = ValidadorCriarPedido.Validar(Comando(3, new ItemComando(7, int.MaxValue), new ItemComando(8, 1), new ItemComando(7, 1)));

        Assert.Equal(["itens[0].quantidade", "itens[2].quantidade"], resultado.Erros.Select(e => e.Campo));
        Assert.All(resultado.Erros, e => Assert.Contains("7", e.Mensagem));
    }

    [Fact]
    public void ConsideraSomenteLinhasValidasAoVerificarOLimiteDeQuantidade()
    {
        var resultado = ValidadorCriarPedido.Validar(Comando(3, new ItemComando(7, int.MaxValue), new ItemComando(7, 0)));

        Assert.Equal(["itens[1].quantidade"], resultado.Erros.Select(e => e.Campo));
    }
}
