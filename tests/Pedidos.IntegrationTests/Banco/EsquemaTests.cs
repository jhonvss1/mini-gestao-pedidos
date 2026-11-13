using Pedidos.IntegrationTests.Infra;

namespace Pedidos.IntegrationTests.Banco;

public class EsquemaTests(BancoDeTeste banco) : IClassFixture<BancoDeTeste>
{
    [Theory]
    [Trait("CA", "CA-37")]
    [InlineData("produto", "preco")]
    [InlineData("item_pedido", "preco_unitario")]
    [InlineData("pedido", "valor_total")]
    public async Task ColunaMonetariaEDecimal18Por2(string tabela, string coluna)
    {
        var tipo = await banco.EscalarAsync<string>($"""
            SELECT DATA_TYPE + '(' + CAST(NUMERIC_PRECISION AS varchar(5)) + ',' + CAST(NUMERIC_SCALE AS varchar(5)) + ')'
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = '{tabela}' AND COLUMN_NAME = '{coluna}'
            """);

        Assert.Equal("decimal(18,2)", tipo);
    }

    [Fact]
    [Trait("CA", "CA-37")]
    public async Task NaoUsaFloatNemMoney()
    {
        var proibidas = await banco.EscalarAsync<int>("""
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'dbo' AND DATA_TYPE IN ('float', 'real', 'money', 'smallmoney')
            """);

        Assert.Equal(0, proibidas);
    }

    [Fact]
    [Trait("CA", "CA-37")]
    public async Task DataCriacaoEDatetime2ComSegundos()
    {
        var tipo = await banco.EscalarAsync<string>("""
            SELECT DATA_TYPE + '(' + CAST(DATETIME_PRECISION AS varchar(5)) + ')'
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'pedido' AND COLUMN_NAME = 'data_criacao'
            """);

        Assert.Equal("datetime2(0)", tipo);
    }
}
