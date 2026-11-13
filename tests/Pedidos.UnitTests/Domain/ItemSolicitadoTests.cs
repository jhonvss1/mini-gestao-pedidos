using Pedidos.Domain;

namespace Pedidos.UnitTests.Domain;

public class ItemSolicitadoTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -3)]
    public void RejeitaProdutoOuQuantidadeNaoPositivos(int produtoId, int quantidade)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemSolicitado(produtoId, quantidade));
    }

    [Trait("CA", "CA-12")]
    [Fact]
    public void ConsolidaLinhasDoMesmoProdutoSomandoAsQuantidades()
    {
        var resultado = ItemSolicitado.Consolidar([new(7, 2), new(7, 3)]);

        Assert.True(resultado.Valido);
        var item = Assert.Single(resultado.Itens);
        Assert.Equal(new ItemSolicitado(7, 5), item);
    }

    [Fact]
    public void MantemItensDeProdutosDistintosEmOrdemCrescenteDeId()
    {
        var resultado = ItemSolicitado.Consolidar([new(9, 1), new(2, 4), new(5, 2), new(2, 1)]);

        Assert.Equal([new ItemSolicitado(2, 5), new ItemSolicitado(5, 2), new ItemSolicitado(9, 1)], resultado.Itens);
    }

    [Fact]
    public void ListaVaziaGeraResultadoVazioEValido()
    {
        var resultado = ItemSolicitado.Consolidar([]);

        Assert.Empty(resultado.Itens);
        Assert.True(resultado.Valido);
    }

    [Trait("CA", "CA-17b")]
    [Fact]
    public void SomaAcimaDeIntMaxEMarcaOProdutoComoExcedido()
    {
        var resultado = ItemSolicitado.Consolidar([new(7, int.MaxValue), new(7, 1), new(8, 3)]);

        Assert.False(resultado.Valido);
        Assert.Equal([7], resultado.ProdutosComQuantidadeExcedida);
        Assert.Equal([new ItemSolicitado(8, 3)], resultado.Itens);
    }

    [Trait("CA", "CA-17b")]
    [Fact]
    public void SomaExatamenteIgualAIntMaxEAceita()
    {
        var resultado = ItemSolicitado.Consolidar([new(7, int.MaxValue - 1), new(7, 1)]);

        Assert.True(resultado.Valido);
        Assert.Equal(new ItemSolicitado(7, int.MaxValue), Assert.Single(resultado.Itens));
    }

    [Fact]
    public void ListaTodosOsProdutosExcedidosEmOrdemCrescente()
    {
        var resultado = ItemSolicitado.Consolidar(
            [new(9, int.MaxValue), new(9, 1), new(3, int.MaxValue), new(3, 2)]);

        Assert.Equal([3, 9], resultado.ProdutosComQuantidadeExcedida);
    }
}
