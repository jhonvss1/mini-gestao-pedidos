namespace Pedidos.Application.Consultas;

/// <summary>Parâmetros já tipados da listagem. Ausente = filtro não aplicado (pagina e tamanho têm padrão).</summary>
public record ListarPedidosConsulta(
    int? ClienteId,
    DateOnly? DataInicio,
    DateOnly? DataFim,
    int? Pagina,
    int? TamanhoPagina);
