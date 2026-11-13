namespace Pedidos.Application;

/// <summary>Mensagens de erro em português, centralizadas. Identificam o campo ou item e a causa (RN-11).</summary>
internal static class Mensagens
{
    public const string ClienteIdInvalido = "O clienteId deve ser um inteiro maior que zero.";
    public const string ItensVazios = "O pedido deve conter ao menos um item.";
    public const string ItemNulo = "O item deve ser informado.";
    public const string ProdutoIdInvalido = "O produtoId deve ser um inteiro maior que zero.";
    public const string QuantidadeInvalida = "A quantidade deve ser um inteiro maior que zero.";
    public const string TotalExcedido = "O valor total do pedido excede o limite monetário permitido.";

    public const string PaginaInvalida = "A página deve ser um inteiro maior ou igual a 1.";
    public const string IntervaloInvalido = "A dataInicio não pode ser posterior à dataFim.";

    public static string TamanhoPaginaInvalido(int maximo) =>
        $"O tamanhoPagina deve ser um inteiro entre 1 e {maximo}.";

    public static string QuantidadeExcedida(int produtoId) =>
        $"A soma das quantidades do produto {produtoId} excede o limite de {int.MaxValue}.";

    public static string SubtotalExcedido(int produtoId) =>
        $"O subtotal do produto {produtoId} excede o limite monetário permitido.";

    public static string ClienteNaoEncontrado(int clienteId) => $"Cliente {clienteId} não encontrado.";

    public static string ProdutoNaoEncontrado(int produtoId, IEnumerable<int> indices) =>
        $"Produto {produtoId} não encontrado ({string.Join(", ", indices.Select(i => $"itens[{i}]"))}).";

    public static string EstoqueInsuficiente(int produtoId, int solicitado, int disponivel) =>
        $"Estoque insuficiente para o produto {produtoId}: solicitado {solicitado}, disponível {disponivel}.";
}
