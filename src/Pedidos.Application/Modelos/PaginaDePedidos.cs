namespace Pedidos.Application.Modelos;

public record PaginaDePedidos(
    IReadOnlyList<PedidoListado> Registros,
    int Pagina,
    int TamanhoPagina,
    int TotalItens,
    int TotalPaginas);
