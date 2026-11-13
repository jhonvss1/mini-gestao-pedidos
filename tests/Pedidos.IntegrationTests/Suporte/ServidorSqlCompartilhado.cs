using Testcontainers.MsSql;

namespace Pedidos.IntegrationTests.Suporte;

/// <summary>
/// Um único SQL Server real por execução de testes, compartilhado por todas as classes.
/// A imagem é a mesma do docker-compose.yml; o Ryuk do Testcontainers remove o container ao final.
/// </summary>
public static class ServidorSqlCompartilhado
{
    // Manter igual à tag do serviço db em docker-compose.yml.
    public const string Imagem = "mcr.microsoft.com/mssql/server:2022-CU28-ubuntu-22.04";

    private static readonly Lazy<Task<MsSqlContainer>> Container = new(IniciarAsync);

    public static async Task<string> ObterStringConexaoAsync()
    {
        var container = await Container.Value;
        return container.GetConnectionString();
    }

    private static async Task<MsSqlContainer> IniciarAsync()
    {
        var container = new MsSqlBuilder(Imagem).Build();
        await container.StartAsync();
        return container;
    }
}
