using Microsoft.AspNetCore.Diagnostics;

namespace Pedidos.Api.Erros;

/// <summary>Última barreira: loga a exceção e responde 500 genérico, sem vazar detalhes internos.</summary>
public sealed class ManipuladorDeExcecoes(ILogger<ManipuladorDeExcecoes> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Erro inesperado em {Metodo} {Caminho}", httpContext.Request.Method, httpContext.Request.Path);

        var problema = RespostasDeErro.InternoInesperado();
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problema, options: null, "application/problem+json", cancellationToken);
        return true;
    }
}
