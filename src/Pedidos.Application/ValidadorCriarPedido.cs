using Pedidos.Domain;

namespace Pedidos.Application;

/// <summary>Resultado da etapa 1: todos os erros da etapa e, se não houver, os itens já consolidados (RN-09).</summary>
public sealed record ResultadoValidacao(IReadOnlyList<Erro> Erros, IReadOnlyList<ItemSolicitado> Itens)
{
    public bool Valido => Erros.Count == 0;
}

/// <summary>Etapa 1 da RN-11 (estrutura e campos): agrega todos os erros da etapa, sem consultar o banco.</summary>
public static class ValidadorCriarPedido
{
    public static ResultadoValidacao Validar(CriarPedidoComando comando)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var erros = new List<Erro>();

        if (comando.ClienteId is null or <= 0)
            erros.Add(Invalido("clienteId", Mensagens.ClienteIdInvalido));

        if (comando.Itens is null || comando.Itens.Count == 0)
            erros.Add(Invalido("itens", Mensagens.ItensVazios));

        var validos = new List<(int Indice, ItemSolicitado Item)>();
        for (var i = 0; i < (comando.Itens?.Count ?? 0); i++)
        {
            var item = comando.Itens![i];
            if (item is null)
            {
                erros.Add(Invalido($"itens[{i}]", Mensagens.ItemNulo));
                continue;
            }

            var produtoValido = item.ProdutoId is > 0;
            var quantidadeValida = item.Quantidade is > 0;
            if (!produtoValido)
                erros.Add(Invalido($"itens[{i}].produtoId", Mensagens.ProdutoIdInvalido));
            if (!quantidadeValida)
                erros.Add(Invalido($"itens[{i}].quantidade", Mensagens.QuantidadeInvalida));

            if (produtoValido && quantidadeValida)
                validos.Add((i, new ItemSolicitado(item.ProdutoId!.Value, item.Quantidade!.Value)));
        }

        // Limite de quantidade consolidada (RN-24): só dá para consolidar as linhas válidas.
        var consolidacao = ItemSolicitado.Consolidar(validos.Select(v => v.Item));
        foreach (var produtoId in consolidacao.ProdutosComQuantidadeExcedida)
        {
            foreach (var (indice, _) in validos.Where(v => v.Item.ProdutoId == produtoId))
                erros.Add(Invalido($"itens[{indice}].quantidade", Mensagens.QuantidadeExcedida(produtoId)));
        }

        return new ResultadoValidacao(erros, consolidacao.Itens);
    }

    private static Erro Invalido(string campo, string mensagem) => new(CodigosErro.Validacao, mensagem, campo);
}
