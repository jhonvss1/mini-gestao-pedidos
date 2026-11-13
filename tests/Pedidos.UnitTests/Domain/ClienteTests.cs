using Pedidos.Domain;

namespace Pedidos.UnitTests.Domain;

public class ClienteTests
{
    [Fact]
    public void ExpoeValoresInformados()
    {
        var cliente = new Cliente(3, "João Souza");

        Assert.Equal(3, cliente.Id);
        Assert.Equal("João Souza", cliente.Nome);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejeitaIdNaoPositivo(int id)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cliente(id, "Maria"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void RejeitaNomeEmBranco(string nome)
    {
        Assert.Throws<ArgumentException>(() => new Cliente(1, nome));
    }
}
