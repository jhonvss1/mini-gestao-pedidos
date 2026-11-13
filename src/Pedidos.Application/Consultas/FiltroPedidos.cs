namespace Pedidos.Application.Consultas;

/// <summary>Filtro para a porta: intervalo de <c>DataCriacao</c> em UTC, início inclusivo e fim exclusivo.</summary>
public sealed record FiltroPedidos(int? ClienteId, DateTime? Inicio, DateTime? FimExclusivo, int Pagina, int TamanhoPagina);
