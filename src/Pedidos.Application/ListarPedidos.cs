namespace Pedidos.Application;

/// <summary>Parâmetros já tipados da listagem. Ausente = filtro não aplicado (pagina e tamanho têm padrão).</summary>
public sealed record ListarPedidosConsulta(
    int? ClienteId,
    DateOnly? DataInicio,
    DateOnly? DataFim,
    int? Pagina,
    int? TamanhoPagina);

/// <summary>Filtro para a porta: intervalo de <c>DataCriacao</c> em UTC, início inclusivo e fim exclusivo.</summary>
public sealed record FiltroPedidos(int? ClienteId, DateTime? Inicio, DateTime? FimExclusivo, int Pagina, int TamanhoPagina);

public sealed record ItemPedidoListado(
    int ProdutoId,
    string ProdutoNome,
    int Quantidade,
    decimal PrecoUnitario,
    decimal Subtotal);

public sealed record PedidoListado(
    int Id,
    int ClienteId,
    string ClienteNome,
    DateTime DataCriacao,
    decimal ValorTotal,
    IReadOnlyList<ItemPedidoListado> Itens);

/// <summary>Resultado bruto da porta: a página pedida e o total após os filtros.</summary>
public sealed record PedidosEncontrados(IReadOnlyList<PedidoListado> Registros, int TotalItens);

public sealed record PaginaDePedidos(
    IReadOnlyList<PedidoListado> Registros,
    int Pagina,
    int TamanhoPagina,
    int TotalItens,
    int TotalPaginas);

public sealed class ListarPedidos(IPedidoConsultas consultas)
{
    public const int PaginaPadrao = 1;
    public const int TamanhoPaginaPadrao = 20;
    public const int TamanhoPaginaMaximo = 100;

    public async Task<Resultado<PaginaDePedidos>> ExecutarAsync(
        ListarPedidosConsulta consulta,
        CancellationToken cancellationToken)
    {
        var pagina = consulta.Pagina ?? PaginaPadrao;
        var tamanho = consulta.TamanhoPagina ?? TamanhoPaginaPadrao;

        var erros = new List<Erro>();
        if (pagina < 1)
            erros.Add(Invalido("pagina", Mensagens.PaginaInvalida));
        if (tamanho is < 1 or > TamanhoPaginaMaximo)
            erros.Add(Invalido("tamanhoPagina", Mensagens.TamanhoPaginaInvalido(TamanhoPaginaMaximo)));
        if (consulta.DataInicio > consulta.DataFim)
            erros.Add(Invalido("dataInicio", Mensagens.IntervaloInvalido));
        if (erros.Count > 0)
            return Resultado<PaginaDePedidos>.RequisicaoInvalida(erros);

        var filtro = new FiltroPedidos(
            consulta.ClienteId,
            Inicio: consulta.DataInicio?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            FimExclusivo: ProximoDia(consulta.DataFim),
            pagina,
            tamanho);

        var encontrados = await consultas.ListarAsync(filtro, cancellationToken);
        var totalPaginas = (int)Math.Ceiling(encontrados.TotalItens / (double)tamanho);

        return Resultado<PaginaDePedidos>.Ok(
            new PaginaDePedidos(encontrados.Registros, pagina, tamanho, encontrados.TotalItens, totalPaginas));
    }

    // dataFim é inclusiva (RN-13): DataCriacao < (dataFim + 1 dia) 00:00Z. Em DateOnly.MaxValue não há dia seguinte,
    // então o intervalo fica aberto no fim.
    private static DateTime? ProximoDia(DateOnly? dataFim) =>
        dataFim is null || dataFim == DateOnly.MaxValue
            ? null
            : dataFim.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    private static Erro Invalido(string campo, string mensagem) => new(CodigosErro.RequisicaoInvalida, mensagem, campo);
}
