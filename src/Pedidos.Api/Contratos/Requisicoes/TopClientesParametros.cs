using Pedidos.Application.Comum;
using Pedidos.Application.Consultas;
using static Pedidos.Api.Contratos.Requisicoes.ConversoresDeConsulta;

namespace Pedidos.Api.Contratos.Requisicoes;

/// <summary>Query string de <c>GET /relatorios/top-clientes</c>: período obrigatório (a Application exige a presença).</summary>
public record TopClientesParametros(string? DataInicio, string? DataFim)
{
    public (ObterTopClientesConsulta? Consulta, IReadOnlyList<Erro> Erros) Converter()
    {
        var erros = new List<Erro>();

        var dataInicio = LerData(DataInicio, "dataInicio", erros);
        var dataFim = LerData(DataFim, "dataFim", erros);

        return erros.Count > 0
            ? (null, erros)
            : (new ObterTopClientesConsulta(dataInicio, dataFim), erros);
    }
}
