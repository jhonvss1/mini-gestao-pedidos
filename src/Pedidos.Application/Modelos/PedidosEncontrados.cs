namespace Pedidos.Application.Modelos;

/// <summary>Resultado bruto da porta: a página pedida e o total após os filtros.</summary>
public sealed record PedidosEncontrados(IReadOnlyList<PedidoListado> Registros, int TotalItens);
