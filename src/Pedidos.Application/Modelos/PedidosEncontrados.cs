namespace Pedidos.Application.Modelos;

/// <summary>Resultado bruto da porta: a página pedida e o total após os filtros.</summary>
public record PedidosEncontrados(IReadOnlyList<PedidoListado> Registros, int TotalItens);
