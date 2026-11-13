using Pedidos.Application.Comum;
using Pedidos.Application.Consultas;
using static Pedidos.Api.Contratos.Requisicoes.ConversoresDeConsulta;

namespace Pedidos.Api.Contratos.Requisicoes;

/// <summary>
/// Query string de <c>GET /pedidos</c>. Todos os parâmetros malformados são reportados juntos em 400.
/// </summary>
public sealed record ListarPedidosParametros(
    string? ClienteId,
    string? DataInicio,
    string? DataFim,
    string? Pagina,
    string? TamanhoPagina)
{
    public (ListarPedidosConsulta? Consulta, IReadOnlyList<Erro> Erros) Converter()
    {
        var erros = new List<Erro>();

        var clienteId = LerInteiro(ClienteId, "clienteId", erros);
        var dataInicio = LerData(DataInicio, "dataInicio", erros);
        var dataFim = LerData(DataFim, "dataFim", erros);
        var pagina = LerInteiro(Pagina, "pagina", erros);
        var tamanhoPagina = LerInteiro(TamanhoPagina, "tamanhoPagina", erros);

        return erros.Count > 0
            ? (null, erros)
            : (new ListarPedidosConsulta(clienteId, dataInicio, dataFim, pagina, tamanhoPagina), erros);
    }
}
