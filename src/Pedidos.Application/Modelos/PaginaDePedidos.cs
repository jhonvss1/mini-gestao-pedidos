namespace Pedidos.Application.Modelos;

public sealed record PaginaDePedidos(
    IReadOnlyList<PedidoListado> Registros,
    int Pagina,
    int TamanhoPagina,
    int TotalItens,
    int TotalPaginas);
