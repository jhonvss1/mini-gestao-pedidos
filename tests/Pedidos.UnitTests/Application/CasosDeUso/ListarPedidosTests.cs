using NSubstitute;
using Pedidos.Application.CasosDeUso;
using Pedidos.Application.Comum;
using Pedidos.Application.Consultas;
using Pedidos.Application.Interfaces;
using Pedidos.Application.Modelos;

namespace Pedidos.UnitTests.Application.CasosDeUso;

public class ListarPedidosTests
{
    private readonly IPedidoConsultas _consultas = Substitute.For<IPedidoConsultas>();
    private readonly ListarPedidos _casoDeUso;

    public ListarPedidosTests()
    {
        _casoDeUso = new ListarPedidos(_consultas);
        _consultas.ListarAsync(Arg.Any<FiltroPedidos>(), Arg.Any<CancellationToken>())
            .Returns(new PedidosEncontrados([], 0));
    }

    private Task<Resultado<PaginaDePedidos>> Executar(ListarPedidosConsulta consulta) =>
        _casoDeUso.ExecutarAsync(consulta, CancellationToken.None);

    private FiltroPedidos FiltroRecebido() =>
        (FiltroPedidos)_consultas.ReceivedCalls().Single().GetArguments()[0]!;

    [Trait("CA", "CA-18")]
    [Fact]
    public async Task UsaPagina1ETamanho20QuandoNaoInformados()
    {
        var resultado = await Executar(new ListarPedidosConsulta(null, null, null, null, null));

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, resultado.Valor!.Pagina);
        Assert.Equal(20, resultado.Valor.TamanhoPagina);
        var filtro = FiltroRecebido();
        Assert.Equal((1, 20), (filtro.Pagina, filtro.TamanhoPagina));
        Assert.Null(filtro.ClienteId);
        Assert.Null(filtro.Inicio);
        Assert.Null(filtro.FimExclusivo);
    }

    [Trait("CA", "CA-20")]
    [Fact]
    public async Task TraduzODiaInicialEFinalParaIntervaloInclusivoEmUtc()
    {
        await Executar(new ListarPedidosConsulta(3, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 12), 2, 5));

        var filtro = FiltroRecebido();
        Assert.Equal(3, filtro.ClienteId);
        Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), filtro.Inicio);
        Assert.Equal(new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc), filtro.FimExclusivo);
        Assert.Equal((2, 5), (filtro.Pagina, filtro.TamanhoPagina));
    }

    [Trait("CA", "CA-21")]
    [Fact]
    public async Task AceitaApenasUmaDasPontasDoIntervalo()
    {
        await Executar(new ListarPedidosConsulta(null, new DateOnly(2026, 11, 1), null, null, null));
        var apenasInicio = FiltroRecebido();
        _consultas.ClearReceivedCalls();

        await Executar(new ListarPedidosConsulta(null, null, new DateOnly(2026, 11, 12), null, null));
        var apenasFim = FiltroRecebido();

        Assert.NotNull(apenasInicio.Inicio);
        Assert.Null(apenasInicio.FimExclusivo);
        Assert.Null(apenasFim.Inicio);
        Assert.NotNull(apenasFim.FimExclusivo);
    }

    [Fact]
    public async Task DeixaOFimAbertoQuandoADataFimEOMaximoSuportado()
    {
        var resultado = await Executar(new ListarPedidosConsulta(null, null, DateOnly.MaxValue, null, null));

        Assert.True(resultado.Sucesso);
        Assert.Null(FiltroRecebido().FimExclusivo);
    }

    [Trait("CA", "CA-22")]
    [Fact]
    public async Task RejeitaDataInicioPosteriorADataFim()
    {
        var resultado = await Executar(
            new ListarPedidosConsulta(null, new DateOnly(2026, 11, 13), new DateOnly(2026, 11, 12), null, null));

        Assert.Equal(TipoResultado.RequisicaoInvalida, resultado.Tipo);
        Assert.Equal("dataInicio", Assert.Single(resultado.Erros).Campo);
        await _consultas.DidNotReceiveWithAnyArgs().ListarAsync(default!, default);
    }

    [Trait("CA", "CA-22")]
    [Theory]
    [InlineData(0, null, "pagina")]
    [InlineData(-1, null, "pagina")]
    [InlineData(null, 0, "tamanhoPagina")]
    [InlineData(null, 101, "tamanhoPagina")]
    [InlineData(null, -5, "tamanhoPagina")]
    public async Task RejeitaPaginacaoForaDosLimites(int? pagina, int? tamanho, string campoEsperado)
    {
        var resultado = await Executar(new ListarPedidosConsulta(null, null, null, pagina, tamanho));

        Assert.Equal(TipoResultado.RequisicaoInvalida, resultado.Tipo);
        Assert.Equal(campoEsperado, Assert.Single(resultado.Erros).Campo);
        await _consultas.DidNotReceiveWithAnyArgs().ListarAsync(default!, default);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task AceitaTamanhoDePaginaNosLimites(int tamanho)
    {
        var resultado = await Executar(new ListarPedidosConsulta(null, null, null, 1, tamanho));

        Assert.True(resultado.Sucesso);
    }

    [Fact]
    public async Task AcumulaOsErrosDeParametrosInvalidos()
    {
        var resultado = await Executar(
            new ListarPedidosConsulta(null, new DateOnly(2026, 11, 13), new DateOnly(2026, 11, 12), 0, 101));

        Assert.Equal(["pagina", "tamanhoPagina", "dataInicio"], resultado.Erros.Select(e => e.Campo));
    }

    [Trait("CA", "CA-24")]
    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(57, 20, 3)]
    public async Task CalculaOTotalDePaginas(int totalItens, int tamanho, int totalPaginasEsperado)
    {
        _consultas.ListarAsync(Arg.Any<FiltroPedidos>(), Arg.Any<CancellationToken>())
            .Returns(new PedidosEncontrados([], totalItens));

        var resultado = await Executar(new ListarPedidosConsulta(null, null, null, 9, tamanho));

        Assert.Equal(totalItens, resultado.Valor!.TotalItens);
        Assert.Equal(totalPaginasEsperado, resultado.Valor.TotalPaginas);
        Assert.Equal(9, resultado.Valor.Pagina);
    }
}
