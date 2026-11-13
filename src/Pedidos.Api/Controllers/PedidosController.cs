using Microsoft.AspNetCore.Mvc;
using Pedidos.Api.Contratos;
using Pedidos.Api.Erros;
using Pedidos.Application;

namespace Pedidos.Api.Controllers;

[ApiController]
[Route("pedidos")]
public class PedidosController(CriarPedido criarPedido) : ControllerBase
{
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
