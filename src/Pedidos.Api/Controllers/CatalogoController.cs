using Microsoft.AspNetCore.Mvc;
using Pedidos.Application.CasosDeUso;
using Pedidos.Application.Modelos;

namespace Pedidos.Api.Controllers;

/// <summary>Leituras auxiliares para os seletores do frontend (spec 4.4, D-5).</summary>
[ApiController]
[Route("api/[controller]")]
public class CatalogoController(ListarCatalogo listarCatalogo) : ControllerBase
{
    [HttpGet("clientes")]
    public async Task<ActionResult<IReadOnlyList<ClienteResumo>>> Clientes(CancellationToken cancellationToken) =>
        Ok(await listarCatalogo.ClientesAsync(cancellationToken));

    [HttpGet("produtos")]
    public async Task<ActionResult<IReadOnlyList<ProdutoResumo>>> Produtos(CancellationToken cancellationToken) =>
        Ok(await listarCatalogo.ProdutosAsync(cancellationToken));
}
