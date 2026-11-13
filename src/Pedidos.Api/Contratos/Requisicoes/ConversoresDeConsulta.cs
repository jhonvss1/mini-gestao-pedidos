using System.Globalization;
using Pedidos.Application.Comum;

namespace Pedidos.Api.Contratos.Requisicoes;

/// <summary>Parsing estrito de parâmetros de query string; vazio equivale a ausente e falhas são acumuladas.</summary>
internal static class ConversoresDeConsulta
{
    public static int? LerInteiro(string? valor, string campo, List<Erro> erros)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        if (int.TryParse(valor, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var numero))
            return numero;

        erros.Add(new Erro(CodigosErro.RequisicaoInvalida, $"O parâmetro {campo} deve ser um número inteiro.", campo));
        return null;
    }

    /// <summary>Somente <c>yyyy-MM-dd</c>, em cultura invariante.</summary>
    public static DateOnly? LerData(string? valor, string campo, List<Erro> erros)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        if (DateOnly.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
            return data;

        erros.Add(new Erro(CodigosErro.RequisicaoInvalida, $"O parâmetro {campo} deve estar no formato yyyy-MM-dd.", campo));
        return null;
    }
}
