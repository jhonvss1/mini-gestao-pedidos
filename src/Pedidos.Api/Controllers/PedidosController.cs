using Microsoft.AspNetCore.Mvc;
using Pedidos.Api.Contratos;
using Pedidos.Api.Erros;
using Pedidos.Application;

namespace Pedidos.Api.Controllers;

[ApiController]
[Route("pedidos")]
public class PedidosController(CriarPedido criarPedido, ListarPedidos listarPedidos) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] ListarPedidosParametros parametros, CancellationToken cancellationToken)
    {
        var (consulta, erros) = parametros.Converter();
        if (consulta is null)
            return RespostasDeErro.DeResultado(Resultado<PaginaDePedidos>.RequisicaoInvalida(erros));

        var resultado = await listarPedidos.ExecutarAsync(consulta, cancellationToken);

        return resultado.Sucesso ? Ok(resultado.Valor) : RespostasDeErro.DeResultado(resultado);
    }

    [HttpPost]
    public async Task<IActionResult> Criar(CriarPedidoRequisicao requisicao, CancellationToken cancellationToken)
    {
        var resultado = await criarPedido.ExecutarAsync(requisicao.ParaComando(), cancellationToken);

        // Sem Location: GET /pedidos/{id} está fora do escopo (D-12).
        return resultado.Sucesso
            ? StatusCode(StatusCodes.Status201Created, PedidoResposta.De(resultado.Valor!))
            : RespostasDeErro.DeResultado(resultado);
    }
}
