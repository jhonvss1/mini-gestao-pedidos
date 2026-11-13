using Pedidos.Domain.Entidades;
using Pedidos.Domain.Resultados;

namespace Pedidos.Application;


public sealed class CriarPedido(IPedidoRepositorio repositorio, TimeProvider relogio)
{
    public async Task<Resultado<Pedido>> ExecutarAsync(CriarPedidoComando comando, CancellationToken cancellationToken)
    {
        var validacao = ValidadorCriarPedido.Validar(comando);
        if (!validacao.Valido)
            return Resultado<Pedido>.Validacao(validacao.Erros);

        var clienteId = comando.ClienteId!.Value;
        if (!await repositorio.ClienteExisteAsync(clienteId, cancellationToken))
        {
            return Resultado<Pedido>.NaoEncontrado(
                [new Erro(CodigosErro.ClienteNaoEncontrado, Mensagens.ClienteNaoEncontrado(clienteId), "clienteId")]);
        }

        var agora = relogio.GetUtcNow().UtcDateTime;
        var resultado = await repositorio.CriarAsync(
            clienteId,
            validacao.Itens,
            produtos => Pedido.Criar(clienteId, agora, validacao.Itens, produtos),
            cancellationToken);

        return resultado switch
        {
            ResultadoCriacaoPedido.Criado criado => Resultado<Pedido>.Ok(criado.Pedido),
            ResultadoCriacaoPedido.ProdutosInexistentes r => Resultado<Pedido>.NaoEncontrado(ErrosDeProdutosInexistentes(r, comando)),
            ResultadoCriacaoPedido.LimiteExcedido r => Resultado<Pedido>.Validacao(ErrosDeLimite(r, comando)),
            ResultadoCriacaoPedido.EstoqueInsuficiente r => Resultado<Pedido>.Conflito(ErrosDeEstoque(r)),
            _ => throw new InvalidOperationException($"Resultado de criação não tratado: {resultado.GetType().Name}."),
        };
    }

    private static List<Erro> ErrosDeProdutosInexistentes(
        ResultadoCriacaoPedido.ProdutosInexistentes resultado,
        CriarPedidoComando comando) =>
        resultado.ProdutoIds
            .Select(id => new Erro(
                CodigosErro.ProdutoNaoEncontrado,
                Mensagens.ProdutoNaoEncontrado(id, IndicesDoProduto(comando, id)),
                Dados: new Dictionary<string, object> { ["produtoId"] = id }))
            .ToList();

    private static List<Erro> ErrosDeLimite(ResultadoCriacaoPedido.LimiteExcedido resultado, CriarPedidoComando comando)
    {
        var erros = new List<Erro>();

        foreach (var produtoId in resultado.ProdutosComSubtotalExcedido)
        {
            foreach (var indice in IndicesDoProduto(comando, produtoId))
            {
                erros.Add(new Erro(
                    CodigosErro.Validacao,
                    Mensagens.SubtotalExcedido(produtoId),
                    $"itens[{indice}].quantidade"));
            }
        }

        if (resultado.TotalExcedido)
            erros.Add(new Erro(CodigosErro.Validacao, Mensagens.TotalExcedido, "itens"));

        return erros;
    }

    private static List<Erro> ErrosDeEstoque(ResultadoCriacaoPedido.EstoqueInsuficiente resultado) =>
        resultado.Produtos
            .Select(f => new Erro(
                CodigosErro.EstoqueInsuficiente,
                Mensagens.EstoqueInsuficiente(f.ProdutoId, f.Solicitado, f.Disponivel),
                Dados: new Dictionary<string, object>
                {
                    ["produtoId"] = f.ProdutoId,
                    ["solicitado"] = f.Solicitado,
                    ["disponivel"] = f.Disponivel,
                }))
            .ToList();

    private static List<int> IndicesDoProduto(CriarPedidoComando comando, int produtoId) =>
        comando.Itens!
            .Select((item, indice) => (item, indice))
            .Where(x => x.item?.ProdutoId == produtoId)
            .Select(x => x.indice)
            .ToList();
}
