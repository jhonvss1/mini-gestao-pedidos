using Microsoft.Data.SqlClient;

namespace Pedidos.IntegrationTests.Suporte;

/// <summary>
/// Banco exclusivo de uma classe de teste, criado a partir dos scripts de database/
/// (os mesmos usados pelo Compose). Classes diferentes não compartilham dados.
/// </summary>
public class BancoDeTeste : IAsyncLifetime
{
    private string _stringConexaoServidor = "";

    public string Nome { get; } = $"pedidos_teste_{Guid.NewGuid():N}";

    public string StringConexao =>
        new SqlConnectionStringBuilder(_stringConexaoServidor) { InitialCatalog = Nome }.ConnectionString;

    public async Task InitializeAsync()
    {
        _stringConexaoServidor = await ServidorSqlCompartilhado.ObterStringConexaoAsync();
        await ExecutarNoServidorAsync($"CREATE DATABASE [{Nome}]");
        await ExecutarScriptAsync("01-schema.sql");
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await ExecutarNoServidorAsync(
            $"ALTER DATABASE [{Nome}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Nome}]");
    }

    /// <summary>Executa um script de database/ no banco de teste (scripts não usam GO).</summary>
    public async Task ExecutarScriptAsync(string nomeArquivo)
    {
        var sql = await File.ReadAllTextAsync(Path.Combine(PastaDatabase(), nomeArquivo));
        await using var conexao = await AbrirAsync();
        await using var comando = new SqlCommand(sql, conexao);
        await comando.ExecuteNonQueryAsync();
    }

    public async Task<SqlConnection> AbrirAsync()
    {
        var conexao = new SqlConnection(StringConexao);
        await conexao.OpenAsync();
        return conexao;
    }

    public async Task<T> EscalarAsync<T>(string sql)
    {
        await using var conexao = await AbrirAsync();
        await using var comando = new SqlCommand(sql, conexao);
        return (T)Convert.ChangeType((await comando.ExecuteScalarAsync())!, typeof(T));
    }

    public Task<int> InserirClienteAsync(string nome = "Cliente de teste") =>
        EscalarAsync<int>($"INSERT INTO dbo.cliente (nome) OUTPUT INSERTED.id VALUES (N'{nome}')");

    public Task<int> InserirProdutoAsync(decimal preco, int estoque, string nome = "Produto de teste") =>
        EscalarAsync<int>(string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"INSERT INTO dbo.produto (nome, preco, estoque) OUTPUT INSERTED.id VALUES (N'{nome}', {preco}, {estoque})"));

    /// <summary>Insere um pedido já consolidado (itens e total coerentes com RN-05), sem mexer no estoque.</summary>
    public async Task<int> InserirPedidoAsync(
        int clienteId,
        DateTime dataCriacao,
        params (int ProdutoId, int Quantidade, decimal PrecoUnitario)[] itens)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var total = itens.Sum(i => i.Quantidade * i.PrecoUnitario);
        var data = dataCriacao.ToString("yyyy-MM-ddTHH:mm:ss", inv);
        var pedidoId = await EscalarAsync<int>(string.Create(
            inv,
            $"INSERT INTO dbo.pedido (cliente_id, data_criacao, valor_total) OUTPUT INSERTED.id VALUES ({clienteId}, '{data}', {total})"));

        foreach (var (produtoId, quantidade, preco) in itens)
        {
            await ErroDoComandoAsync(string.Create(
                inv,
                $"INSERT INTO dbo.item_pedido (pedido_id, produto_id, quantidade, preco_unitario) VALUES ({pedidoId}, {produtoId}, {quantidade}, {preco})"));
        }

        return pedidoId;
    }

    public Task<int> EstoqueAsync(int produtoId) =>
        EscalarAsync<int>($"SELECT estoque FROM dbo.produto WHERE id = {produtoId}");

    /// <summary>Executa um comando e devolve o erro do SQL Server (null se não falhou).</summary>
    public async Task<SqlException?> ErroDoComandoAsync(string sql)
    {
        try
        {
            await using var conexao = await AbrirAsync();
            await using var comando = new SqlCommand(sql, conexao);
            await comando.ExecuteNonQueryAsync();
            return null;
        }
        catch (SqlException ex)
        {
            return ex;
        }
    }

    private async Task ExecutarNoServidorAsync(string sql)
    {
        await using var conexao = new SqlConnection(_stringConexaoServidor);
        await conexao.OpenAsync();
        await using var comando = new SqlCommand(sql, conexao);
        await comando.ExecuteNonQueryAsync();
    }

    private static string PastaDatabase()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "Pedidos.slnx")))
            pasta = pasta.Parent;

        return pasta is null
            ? throw new DirectoryNotFoundException("Pedidos.slnx não encontrado acima de " + AppContext.BaseDirectory)
            : Path.Combine(pasta.FullName, "database");
    }
}
