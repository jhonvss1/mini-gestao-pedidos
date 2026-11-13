namespace Pedidos.Application;

/// <summary>Traduz datas inclusivas <c>yyyy-MM-dd</c> para o intervalo de <c>DataCriacao</c> em UTC (RN-13).</summary>
internal static class PeriodoDeDatas
{
    public static DateTime? Inicio(DateOnly? dataInicio) =>
        dataInicio?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    // dataFim é inclusiva: DataCriacao < (dataFim + 1 dia) 00:00Z. Em DateOnly.MaxValue não há dia seguinte,
    // então o intervalo fica aberto no fim.
    public static DateTime? FimExclusivo(DateOnly? dataFim) =>
        dataFim is null || dataFim == DateOnly.MaxValue
            ? null
            : dataFim.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}
