using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Pedidos.Infrastructure.Persistencia.Contexto;
using Pedidos.IntegrationTests.Infra;

namespace Pedidos.IntegrationTests.Banco;

/// <summary>O mapeamento do EF Core precisa continuar compatível com os scripts de database/.</summary>
public class MapeamentoEfTests(BancoDeTeste banco) : IClassFixture<BancoDeTeste>, IAsyncLifetime
{
    public Task InitializeAsync() => banco.ExecutarScriptAsync("02-seed.sql");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ModeloDoEfCorrespondeAoSchemaDosScripts()
    {
        await using var contexto = CriarContexto();

        var doModelo = ColunasDoModelo(contexto);
        var doBanco = await ColunasDoBancoAsync();

        Assert.Equal(doBanco, doModelo);
    }

    [Fact]
    public async Task LePedidoComItensEPreservaDinheiroEUtc()
    {
        await using var contexto = CriarContexto();

        var pedido = await contexto.Pedidos.Include(p => p.Itens).SingleAsync(p => p.Id == 1);

        Assert.Equal(1877.90m, pedido.ValorTotal);
        Assert.Equal(2, pedido.Itens.Count);
        Assert.Equal(new DateTime(2026, 10, 3, 10, 15, 0, DateTimeKind.Utc), pedido.DataCriacao);
        Assert.Equal(DateTimeKind.Utc, pedido.DataCriacao.Kind);
        Assert.Equal(1877.90m, pedido.Itens.Sum(i => i.Subtotal));
    }

    [Fact]
    public async Task LeClienteEProduto()
    {
        await using var contexto = CriarContexto();

        var cliente = await contexto.Clientes.SingleAsync(c => c.Id == 3);
        var produto = await contexto.Produtos.SingleAsync(p => p.Id == 4);

        Assert.Equal("João Souza", cliente.Nome);
        Assert.Equal(149.50m, produto.Preco);
        Assert.Equal(5, produto.Estoque);
    }

    private PedidosDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<PedidosDbContext>().UseSqlServer(banco.StringConexao).Options);

    private static List<string> ColunasDoModelo(PedidosDbContext contexto)
    {
        var modelo = contexto.GetService<IDesignTimeModel>().Model;
        var colunas = new List<string>();

        foreach (var entidade in modelo.GetEntityTypes())
        {
            var tabela = StoreObjectIdentifier.Table(entidade.GetTableName()!, entidade.GetSchema());
            foreach (var propriedade in entidade.GetProperties())
            {
                var tipo = propriedade.GetColumnType(tabela).Replace(" ", "").ToLowerInvariant();
                var nulo = propriedade.IsNullable ? "null" : "not null";
                colunas.Add($"{tabela.Name}.{propriedade.GetColumnName(tabela)}:{tipo}:{nulo}");
            }
        }

        return [.. colunas.Order()];
    }

    private async Task<List<string>> ColunasDoBancoAsync()
    {
        await using var conexao = await banco.AbrirAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            SELECT TABLE_NAME + '.' + COLUMN_NAME + ':' +
                   CASE
                       WHEN DATA_TYPE IN ('nvarchar', 'varchar') THEN DATA_TYPE + '(' + CAST(CHARACTER_MAXIMUM_LENGTH AS varchar(10)) + ')'
                       WHEN DATA_TYPE = 'decimal' THEN 'decimal(' + CAST(NUMERIC_PRECISION AS varchar(5)) + ',' + CAST(NUMERIC_SCALE AS varchar(5)) + ')'
                       WHEN DATA_TYPE = 'datetime2' THEN 'datetime2(' + CAST(DATETIME_PRECISION AS varchar(5)) + ')'
                       ELSE DATA_TYPE
                   END + ':' + CASE IS_NULLABLE WHEN 'YES' THEN 'null' ELSE 'not null' END
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'dbo'
            """;

        var colunas = new List<string>();
        await using var leitor = await comando.ExecuteReaderAsync();
        while (await leitor.ReadAsync())
            colunas.Add(leitor.GetString(0).ToLowerInvariant());

        return [.. colunas.Order()];
    }
}
