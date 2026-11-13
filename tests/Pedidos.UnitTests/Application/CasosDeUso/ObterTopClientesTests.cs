using NSubstitute;
using Pedidos.Application.CasosDeUso;
using Pedidos.Application.Comum;
using Pedidos.Application.Consultas;
using Pedidos.Application.Interfaces;
using Pedidos.Application.Modelos;

namespace Pedidos.UnitTests.Application.CasosDeUso;

public class ObterTopClientesTests
{
    private readonly IPedidoConsultas _consultas = Substitute.For<IPedidoConsultas>();
    private readonly ObterTopClientes _casoDeUso;

    public ObterTopClientesTests()
    {
        _casoDeUso = new ObterTopClientes(_consultas);
        _consultas
            .ObterTopClientesAsync(Arg.Any<DateTime>(), Arg.Any<DateTime?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private Task<Resultado<IReadOnlyList<ClienteRanking>>> Executar(DateOnly? inicio, DateOnly? fim) =>
        _casoDeUso.ExecutarAsync(new ObterTopClientesConsulta(inicio, fim), CancellationToken.None);

    [Trait("CA", "CA-29")]
    [Fact]
    public async Task ConsultaOPeriodoInclusivoEmUtcComLimiteDeCinco()
    {
        await Executar(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        await _consultas.Received(1).ObterTopClientesAsync(
            new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            5,
            Arg.Any<CancellationToken>());
    }

    [Trait("CA", "CA-30")]
    [Fact]
    public async Task RepassaORankingEncontradoSemAlterar()
    {
        IReadOnlyList<ClienteRanking> ranking = [new(3, "Maria Silva", 1520.40m), new(5, "João Souza", 1310m)];
        _consultas
            .ObterTopClientesAsync(Arg.Any<DateTime>(), Arg.Any<DateTime?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ranking);

        var resultado = await Executar(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        Assert.True(resultado.Sucesso);
        Assert.Equal(ranking, resultado.Valor);
    }

    [Trait("CA", "CA-31")]
    [Theory]
    [InlineData(false, true, "dataInicio")]
    [InlineData(true, false, "dataFim")]
    public async Task ExigeAsDuasDatas(bool temInicio, bool temFim, string campoAusente)
    {
        var resultado = await Executar(
            temInicio ? new DateOnly(2026, 10, 1) : null,
            temFim ? new DateOnly(2026, 10, 31) : null);

        Assert.Equal(TipoResultado.RequisicaoInvalida, resultado.Tipo);
        Assert.Equal(campoAusente, Assert.Single(resultado.Erros).Campo);
    }

    [Trait("CA", "CA-31")]
    [Fact]
    public async Task ReportaAsDuasDatasAusentesJuntas()
    {
        var resultado = await Executar(null, null);

        Assert.Equal(["dataInicio", "dataFim"], resultado.Erros.Select(e => e.Campo));
        await _consultas.DidNotReceiveWithAnyArgs().ObterTopClientesAsync(default, default, default, default);
    }

    [Trait("CA", "CA-31")]
    [Fact]
    public async Task RejeitaDataInicioPosteriorADataFim()
    {
        var resultado = await Executar(new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 1));

        Assert.Equal(TipoResultado.RequisicaoInvalida, resultado.Tipo);
        await _consultas.DidNotReceiveWithAnyArgs().ObterTopClientesAsync(default, default, default, default);
    }
}
