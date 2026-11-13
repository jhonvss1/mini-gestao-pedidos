using Microsoft.AspNetCore.Mvc;
using Pedidos.Api.Contratos.Requisicoes;
using Pedidos.Api.Erros;
using Pedidos.Application.CasosDeUso;
using Pedidos.Application.Comum;
using Pedidos.Application.Modelos;

namespace Pedidos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RelatoriosController(ObterTopClientes obterTopClientes) : ControllerBase
{
    [HttpGet("top-clientes")]
    public async Task<IActionResult> TopClientes([FromQuery] TopClientesParametros parametros, CancellationToken cancellationToken)
    {
        var (consulta, erros) = parametros.Converter();
        if (consulta is null)
            return RespostasDeErro.DeResultado(Resultado<IReadOnlyList<ClienteRanking>>.RequisicaoInvalida(erros));

        var resultado = await obterTopClientes.ExecutarAsync(consulta, cancellationToken);

        return resultado.Sucesso ? Ok(resultado.Valor) : RespostasDeErro.DeResultado(resultado);
    }
}
