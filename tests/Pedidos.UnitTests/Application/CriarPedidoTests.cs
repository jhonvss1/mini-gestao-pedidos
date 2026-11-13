using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Pedidos.Application.CasosDeUso;
using Pedidos.Application.Comandos;
using Pedidos.Application.Comum;
using Pedidos.Application.Interfaces;
using Pedidos.Domain.Constantes;
using Pedidos.Domain.Entidades;
using Pedidos.Domain.ObjetosDeValor;
using Pedidos.Domain.Resultados;

namespace Pedidos.UnitTests.Application;

public class CriarPedidoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 13, 10, 30, 15, 789, TimeSpan.Zero);

    private readonly IPedidoRepositorio _repositorio = Substitute.For<IPedidoRepositorio>();
    private readonly CriarPedido _casoDeUso;

    public CriarPedidoTests()
    {
        var relogio = new FakeTimeProvider(Agora);
        _casoDeUso = new CriarPedido(_repositorio, relogio);
        _repositorio.ClienteExisteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    private static CriarPedidoComando Comando(int? clienteId, params ItemComando[] itens) =>
        new CriarPedidoComando(clienteId, itens);

    /// <summary>Faz o repositório executar a decisão de domínio sobre os produtos informados, como a Infrastructure faz.</summary>
    private void RepositorioDecideSobre(params Produto[] produtos) =>
        _repositorio
            .CriarAsync(
                Arg.Any<int>(),
                Arg.Any<IReadOnlyList<ItemSolicitado>>(),
                Arg.Any<Func<IReadOnlyCollection<Produto>, ResultadoCriacaoPedido>>(),
                Arg.Any<CancellationToken>())
            .Returns(chamada => chamada.ArgAt<Func<IReadOnlyCollection<Produto>, ResultadoCriacaoPedido>>(2)(produtos));

    private void RepositorioDevolve(ResultadoCriacaoPedido resultado) =>
        _repositorio
            .CriarAsync(
                Arg.Any<int>(),
                Arg.Any<IReadOnlyList<ItemSolicitado>>(),
                Arg.Any<Func<IReadOnlyCollection<Produto>, ResultadoCriacaoPedido>>(),
                Arg.Any<CancellationToken>())
            .Returns(resultado);

    private Task<Resultado<Pedido>> Executar(CriarPedidoComando comando) =>
        _casoDeUso.ExecutarAsync(comando, CancellationToken.None);

    [Fact]
    public async Task CriaPedidoComPrecoDoCadastroEDataDoRelogioTruncada()
    {
        RepositorioDecideSobre(new Produto(7, "Teclado", 89.90m, 10), new Produto(9, "Mouse", 79.90m, 10));

        var resultado = await Executar(Comando(3, new ItemComando(7, 2), new ItemComando(9, 1)));

        Assert.True(resultado.Sucesso);
        var pedido = resultado.Valor!;
        Assert.Equal(3, pedido.ClienteId);
        Assert.Equal(259.70m, pedido.ValorTotal);
        Assert.Equal(new DateTime(2026, 11, 13, 10, 30, 15, DateTimeKind.Utc), pedido.DataCriacao);
    }

    [Trait("CA", "CA-12")]
    [Fact]
    public async Task EnviaAoRepositorioOsItensConsolidados()
    {
        RepositorioDecideSobre(new Produto(7, "Item", 10m, 5));

        var resultado = await Executar(Comando(3, new ItemComando(7, 2), new ItemComando(7, 3)));

        Assert.Equal(5, Assert.Single(resultado.Valor!.Itens).Quantidade);
        await _repositorio.Received(1).CriarAsync(
            3,
            Arg.Is<IReadOnlyList<ItemSolicitado>>(itens => itens.SequenceEqual(new[] { new ItemSolicitado(7, 5) })),
            Arg.Any<Func<IReadOnlyCollection<Produto>, ResultadoCriacaoPedido>>(),
            Arg.Any<CancellationToken>());
    }

    [Trait("CA", "CA-05c")]
    [Fact]
    public async Task ValidacaoDaEtapa1PrevaleceSobreClienteInexistente()
    {
        _repositorio.ClienteExisteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(false);

        var resultado = await Executar(Comando(99, new ItemComando(7, 0)));

        Assert.Equal(TipoResultado.Validacao, resultado.Tipo);
        await _repositorio.DidNotReceiveWithAnyArgs().ClienteExisteAsync(default, default);
        await _repositorio.DidNotReceiveWithAnyArgs().CriarAsync(default, default!, default!, default);
    }

    [Trait("CA", "CA-04")]
    [Fact]
    public async Task RetornaNaoEncontradoParaClienteInexistenteSemTocarEmProdutos()
    {
        _repositorio.ClienteExisteAsync(99, Arg.Any<CancellationToken>()).Returns(false);

        var resultado = await Executar(Comando(99, new ItemComando(7, 1)));

        Assert.Equal(TipoResultado.NaoEncontrado, resultado.Tipo);
        var erro = Assert.Single(resultado.Erros);
        Assert.Equal(CodigosErro.ClienteNaoEncontrado, erro.Codigo);
        await _repositorio.DidNotReceiveWithAnyArgs().CriarAsync(default, default!, default!, default);
    }

    [Trait("CA", "CA-05b")]
    [Fact]
    public async Task ListaTodosOsProdutosInexistentesComOsItensDeOrigem()
    {
        RepositorioDecideSobre(new Produto(7, "Teclado", 89.90m, 10));

        var resultado = await Executar(
            Comando(3, new ItemComando(7, 1), new ItemComando(99, 1), new ItemComando(98, 2), new ItemComando(99, 3)));

        Assert.Equal(TipoResultado.NaoEncontrado, resultado.Tipo);
        Assert.Equal([98, 99], resultado.Erros.Select(e => e.Dados!["produtoId"]));
        Assert.All(resultado.Erros, e => Assert.Equal(CodigosErro.ProdutoNaoEncontrado, e.Codigo));
        Assert.Contains("itens[1], itens[3]", resultado.Erros[1].Mensagem);
    }

    [Trait("CA", "CA-06")]
    [Fact]
    public async Task RetornaConflitoComSolicitadoEDisponivelDeTodosOsProdutosAfetados()
    {
        RepositorioDecideSobre(new Produto(7, "A", 10m, 3), new Produto(9, "B", 10m, 0), new Produto(5, "C", 10m, 50));

        var resultado = await Executar(Comando(3, new ItemComando(7, 5), new ItemComando(9, 1), new ItemComando(5, 2)));

        Assert.Equal(TipoResultado.Conflito, resultado.Tipo);
        Assert.Equal(2, resultado.Erros.Count);
        var primeiro = resultado.Erros[0];
        Assert.Equal(CodigosErro.EstoqueInsuficiente, primeiro.Codigo);
        Assert.Equal(7, primeiro.Dados!["produtoId"]);
        Assert.Equal(5, primeiro.Dados["solicitado"]);
        Assert.Equal(3, primeiro.Dados["disponivel"]);
    }

    [Trait("CA", "CA-05c")]
    [Fact]
    public async Task ProdutoInexistenteTemPrioridadeSobreEstoqueInsuficiente()
    {
        RepositorioDecideSobre(new Produto(7, "Sem saldo", 10m, 0));

        var resultado = await Executar(Comando(3, new ItemComando(7, 1), new ItemComando(99, 1)));

        Assert.Equal(TipoResultado.NaoEncontrado, resultado.Tipo);
    }

    [Trait("CA", "CA-17c")]
    [Fact]
    public async Task RetornaValidacaoQuandoOSubtotalExcedeOLimiteMonetario()
    {
        RepositorioDecideSobre(new Produto(7, "Caro", Limites.ValorMonetarioMaximo, 10));

        var resultado = await Executar(Comando(3, new ItemComando(7, 1), new ItemComando(7, 1)));

        Assert.Equal(TipoResultado.Validacao, resultado.Tipo);
        Assert.Equal(["itens[0].quantidade", "itens[1].quantidade"], resultado.Erros.Select(e => e.Campo));
    }

    [Trait("CA", "CA-17c")]
    [Fact]
    public async Task RetornaValidacaoQuandoOTotalExcedeOLimiteMonetario()
    {
        RepositorioDecideSobre(
            new Produto(7, "A", 6_000_000_000_000_000m, 10),
            new Produto(9, "B", 6_000_000_000_000_000m, 10));

        var resultado = await Executar(Comando(3, new ItemComando(7, 1), new ItemComando(9, 1)));

        Assert.Equal(TipoResultado.Validacao, resultado.Tipo);
        Assert.Equal("itens", Assert.Single(resultado.Erros).Campo);
    }

    [Fact]
    public async Task RepassaOResultadoDeDisputaPerdidaComoConflito()
    {
        RepositorioDevolve(new ResultadoCriacaoPedido.EstoqueInsuficiente([new FaltaDeEstoque(7, 1, 0)]));

        var resultado = await Executar(Comando(3, new ItemComando(7, 1)));

        Assert.Equal(TipoResultado.Conflito, resultado.Tipo);
    }
}
