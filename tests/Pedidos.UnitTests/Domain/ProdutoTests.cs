using Pedidos.Domain;

namespace Pedidos.UnitTests.Domain;

public class ProdutoTests
{
    [Theory]
    [InlineData(5, 1, true)]
    [InlineData(5, 5, true)]
    [InlineData(5, 6, false)]
    [InlineData(0, 1, false)]
    [InlineData(int.MaxValue, int.MaxValue, true)]
    public void TemEstoque_compara_saldo_com_quantidade(int estoque, int quantidade, bool esperado)
    {
        var produto = new Produto(1, "Teclado", 89.90m, estoque);

        Assert.Equal(esperado, produto.TemEstoque(quantidade));
    }

    [Fact]
    public void TemEstoque_nao_altera_o_saldo()
    {
        var produto = new Produto(1, "Teclado", 89.90m, 5);

        produto.TemEstoque(3);

        Assert.Equal(5, produto.Estoque);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TemEstoque_rejeita_quantidade_nao_positiva(int quantidade)
    {
        var produto = new Produto(1, "Teclado", 89.90m, 5);

        Assert.Throws<ArgumentOutOfRangeException>(() => produto.TemEstoque(quantidade));
    }

    [Fact]
    public void Expoe_os_valores_informados()
    {
        var produto = new Produto(7, "Hub USB", 59.90m, 80);

        Assert.Equal(7, produto.Id);
        Assert.Equal("Hub USB", produto.Nome);
        Assert.Equal(59.90m, produto.Preco);
        Assert.Equal(80, produto.Estoque);
    }

    [Fact]
    public void Aceita_estoque_zero()
    {
        var produto = new Produto(5, "Webcam HD", 199.90m, 0);

        Assert.False(produto.TemEstoque(1));
    }

    [Theory]
    [InlineData(0, "Teclado", 10, 1)]
    [InlineData(-1, "Teclado", 10, 1)]
    [InlineData(1, "Teclado", 0, 1)]
    [InlineData(1, "Teclado", -0.01, 1)]
    [InlineData(1, "Teclado", 10, -1)]
    public void Rejeita_valores_invalidos_numericos(int id, string nome, double preco, int estoque)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Produto(id, nome, (decimal)preco, estoque));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejeita_nome_em_branco(string nome)
    {
        Assert.Throws<ArgumentException>(() => new Produto(1, nome, 10m, 1));
    }
}
