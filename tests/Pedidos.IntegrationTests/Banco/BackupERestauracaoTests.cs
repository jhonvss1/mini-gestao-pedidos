using Microsoft.Data.SqlClient;
using Pedidos.IntegrationTests.Suporte;

namespace Pedidos.IntegrationTests.Banco;

/// <summary>
/// Valida os comandos T-SQL de scripts/backup-banco.sh e scripts/restaurar-banco.sh num SQL Server real: backup com
/// CHECKSUM, RESTORE VERIFYONLY e restauração em outro banco com os mesmos MOVE. O banco precisa se chamar
/// gestao_pedidos (como em produção) para os nomes lógicos dos arquivos baterem com os do script.
/// Os comandos abaixo espelham os dos scripts; se um mudar, o outro deve mudar junto.
/// </summary>
public class BackupERestauracaoTests : IAsyncLifetime
{
    private const string Origem = "gestao_pedidos";
    private const string Destino = "gestao_pedidos_verificacao";
    private const string Arquivo = "/var/opt/mssql/data/gestao_pedidos_teste.bak";

    private string _servidor = "";

    public async Task InitializeAsync()
    {
        _servidor = await ServidorSqlCompartilhado.ObterStringConexaoAsync();
        await ExecutarAsync("master", ScriptDatabase("00-create-database.sql"));
        await ExecutarAsync(Origem, ScriptDatabase("01-schema.sql"));
        await ExecutarAsync(Origem, ScriptDatabase("02-seed.sql"));
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        foreach (var banco in new[] { Origem, Destino })
        {
            await ExecutarAsync(
                "master",
                $"IF DB_ID(N'{banco}') IS NOT NULL BEGIN ALTER DATABASE [{banco}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{banco}]; END");
        }
    }

    [Fact]
    public async Task BackupVerificadoRestauraOsMesmosDadosEmOutroBanco()
    {
        var pedidosAntes = await ContarPedidosAsync(Origem);

        await ExecutarAsync("master", $"BACKUP DATABASE {Origem} TO DISK = N'{Arquivo}' WITH CHECKSUM, INIT, NAME = N'gestao_pedidos teste';");
        await ExecutarAsync("master", $"RESTORE VERIFYONLY FROM DISK = N'{Arquivo}' WITH CHECKSUM;");
        await ExecutarAsync(
            "master",
            $"""
            RESTORE DATABASE [{Destino}] FROM DISK = N'{Arquivo}' WITH REPLACE, CHECKSUM,
              MOVE N'gestao_pedidos' TO N'/var/opt/mssql/data/{Destino}.mdf',
              MOVE N'gestao_pedidos_log' TO N'/var/opt/mssql/data/{Destino}_log.ldf';
            """);

        Assert.True(pedidosAntes > 0);
        Assert.Equal(pedidosAntes, await ContarPedidosAsync(Destino));
        Assert.Equal(
            await SomarTotaisAsync(Origem),
            await SomarTotaisAsync(Destino));
    }

    private async Task<int> ContarPedidosAsync(string banco) =>
        Convert.ToInt32(await EscalarAsync(banco, "SELECT COUNT(*) FROM dbo.pedido"));

    private async Task<decimal> SomarTotaisAsync(string banco) =>
        Convert.ToDecimal(await EscalarAsync(banco, "SELECT SUM(valor_total) FROM dbo.pedido"));

    private async Task ExecutarAsync(string banco, string sql)
    {
        await using var conexao = new SqlConnection(new SqlConnectionStringBuilder(_servidor) { InitialCatalog = banco }.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = new SqlCommand(sql, conexao) { CommandTimeout = 120 };
        await comando.ExecuteNonQueryAsync();
    }

    private async Task<object> EscalarAsync(string banco, string sql)
    {
        await using var conexao = new SqlConnection(new SqlConnectionStringBuilder(_servidor) { InitialCatalog = banco }.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = new SqlCommand(sql, conexao);
        return (await comando.ExecuteScalarAsync())!;
    }

    private static string ScriptDatabase(string nome)
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "Pedidos.slnx")))
            pasta = pasta.Parent;

        return File.ReadAllText(Path.Combine(pasta!.FullName, "database", nome));
    }
}
