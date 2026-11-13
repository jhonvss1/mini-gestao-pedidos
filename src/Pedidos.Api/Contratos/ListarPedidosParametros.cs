using System.Globalization;
using Pedidos.Application;

namespace Pedidos.Api.Contratos;

/// <summary>
/// Parsing estrito da query string de <c>GET /pedidos</c>: datas só em <c>yyyy-MM-dd</c> (cultura invariante) e inteiros
/// numéricos. Todos os parâmetros malformados são reportados juntos em 400. Valor vazio equivale a parâmetro ausente.
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

    private static int? LerInteiro(string? valor, string campo, List<Erro> erros)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        if (int.TryParse(valor, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var numero))
            return numero;

        erros.Add(new Erro(CodigosErro.RequisicaoInvalida, $"O parâmetro {campo} deve ser um número inteiro.", campo));
        return null;
    }

    private static DateOnly? LerData(string? valor, string campo, List<Erro> erros)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        if (DateOnly.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
            return data;

        erros.Add(new Erro(CodigosErro.RequisicaoInvalida, $"O parâmetro {campo} deve estar no formato yyyy-MM-dd.", campo));
        return null;
    }
}
