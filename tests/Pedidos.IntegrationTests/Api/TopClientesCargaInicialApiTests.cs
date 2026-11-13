using System.Net.Http.Json;
using System.Text.Json;
using Pedidos.IntegrationTests.Suporte;

namespace Pedidos.IntegrationTests.Api;

/// <summary>Ranking sobre a carga inicial real, conferido contra um cálculo independente (soma dos itens).</summary>
public class TopClientesCargaInicialApiTests(BancoDeTeste banco) : IClassFixture<BancoDeTeste>, IAsyncLifetime, IDisposable
{
    private readonly FabricaApi _fabrica = new(banco.StringConexao);

    public Task InitializeAsync() => banco.ExecutarScriptAsync("02-seed.sql");

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _fabrica.Dispose();

    [Trait("CA", "CA-32")]
    [Fact]
    public async Task TotaisDeOutubroBatemComASomaIndependenteDosItens()
    {
        var corpo = await _fabrica.CreateClient()
            .GetFromJsonAsync<JsonElement>("/api/relatorios/top-clientes?dataInicio=2026-10-01&dataFim=2026-10-31");
        var ranking = corpo.EnumerateArray()
            .Select(e => (ClienteId: e.GetProperty("clienteId").GetInt32(), Total: e.GetProperty("valorTotal").GetDecimal()))
            .ToList();

        // Cálculo independente: soma quantidade x preço dos itens, sem usar pedido.valor_total.
        var esperado = new List<(int ClienteId, decimal Total)>();
        await using (var conexao = await banco.AbrirAsync())
        await using (var comando = conexao.CreateCommand())
        {
            comando.CommandText = """
                SELECT TOP 5 p.cliente_id, SUM(i.quantidade * i.preco_unitario) AS total
                FROM dbo.pedido p JOIN dbo.item_pedido i ON i.pedido_id = p.id
                WHERE p.data_criacao >= '2026-10-01' AND p.data_criacao < '2026-11-01'
                GROUP BY p.cliente_id
                ORDER BY total DESC, p.cliente_id ASC
                """;
            await using var leitor = await comando.ExecuteReaderAsync();
            while (await leitor.ReadAsync())
                esperado.Add((leitor.GetInt32(0), leitor.GetDecimal(1)));
        }

        Assert.Equal(5, ranking.Count);
        Assert.Equal(esperado, ranking);
        // Maria Silva (1), os dois João Souza (3 e 5) e o empate Ana Lima (2) / Pedro Alves (4), resolvido por id.
        Assert.Equal([1, 3, 5, 2, 4], ranking.Select(r => r.ClienteId));
        Assert.Equal([1977.50m, 1288.50m, 899.00m, 448.40m, 448.40m], ranking.Select(r => r.Total));
    }
}
