using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Pedidos.Application.Comum;

namespace Pedidos.Api.Erros;

/// <summary>Traduz o <see cref="Resultado"/> da Application para o Problem Details da spec 4.1 (campo <c>codigo</c> estável).</summary>
public static class RespostasDeErro
{
    public static ObjectResult DeResultado(Resultado resultado)
    {
        var erros = resultado.Erros;
        var primeiro = erros[0];

        var problema = resultado.Tipo switch
        {
            TipoResultado.Validacao => Novo(
                StatusCodes.Status422UnprocessableEntity,
                "Entidade não processável",
                CodigosErro.Validacao,
                detalhe: null,
                ("errors", AgruparPorCampo(erros))),

            TipoResultado.RequisicaoInvalida => Novo(
                StatusCodes.Status400BadRequest,
                "Requisição inválida",
                CodigosErro.RequisicaoInvalida,
                detalhe: null,
                ("errors", AgruparPorCampo(erros))),

            TipoResultado.NaoEncontrado when primeiro.Codigo == CodigosErro.ProdutoNaoEncontrado => Novo(
                StatusCodes.Status404NotFound,
                "Produto não encontrado",
                CodigosErro.ProdutoNaoEncontrado,
                "Um ou mais produtos informados não existem.",
                ("produtos", erros.Select(e => new { produtoId = e.Dados!["produtoId"], mensagem = e.Mensagem }).ToList())),

            TipoResultado.NaoEncontrado => Novo(
                StatusCodes.Status404NotFound,
                "Cliente não encontrado",
                primeiro.Codigo,
                primeiro.Mensagem),

            TipoResultado.Conflito => Novo(
                StatusCodes.Status409Conflict,
                "Estoque insuficiente",
                CodigosErro.EstoqueInsuficiente,
                "Um ou mais produtos não possuem estoque suficiente.",
                ("produtos", erros.Select(e => new
                {
                    produtoId = e.Dados!["produtoId"],
                    solicitado = e.Dados["solicitado"],
                    disponivel = e.Dados["disponivel"],
                }).ToList())),

            _ => throw new InvalidOperationException($"Resultado sem tradução HTTP: {resultado.Tipo}."),
        };

        return new ObjectResult(problema) { StatusCode = problema.Status };
    }

    /// <summary>Falha de formato (corpo ausente, JSON inválido, tipo errado): 400 sem expor detalhes internos do parser.</summary>
    public static ObjectResult DeModelStateInvalido(ModelStateDictionary modelState)
    {
        var erros = modelState
            .Where(entrada => entrada.Value is { Errors.Count: > 0 })
            .Select(entrada => entrada.Key.TrimStart('$', '.') is { Length: > 0 } campo
                ? new Erro(CodigosErro.RequisicaoInvalida, "Valor inválido ou em formato incorreto.", campo)
                : new Erro(CodigosErro.RequisicaoInvalida, "O corpo da requisição é obrigatório e deve ser um JSON válido.", "corpo"))
            .ToList();

        if (erros.Count == 0)
            erros.Add(new Erro(CodigosErro.RequisicaoInvalida, "Requisição malformada.", "corpo"));

        return DeResultado(Resultado<object>.RequisicaoInvalida(erros));
    }

    public static ProblemDetails InternoInesperado() =>
        Novo(StatusCodes.Status500InternalServerError, "Erro interno", codigo: null, "Ocorreu um erro inesperado.");

    private static Dictionary<string, string[]> AgruparPorCampo(IEnumerable<Erro> erros) =>
        erros
            .GroupBy(e => e.Campo ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Mensagem).ToArray());

    private static ProblemDetails Novo(
        int status,
        string titulo,
        string? codigo,
        string? detalhe,
        params (string Chave, object Valor)[] extensoes)
    {
        var problema = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}",
            Title = titulo,
            Status = status,
            Detail = detalhe,
        };

        if (codigo is not null)
            problema.Extensions["codigo"] = codigo;
        foreach (var (chave, valor) in extensoes)
            problema.Extensions[chave] = valor;

        return problema;
    }
}
