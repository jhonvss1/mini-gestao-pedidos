namespace Pedidos.Application;

/// <summary>Códigos estáveis de erro (spec 4.1), expostos no campo <c>codigo</c> do Problem Details.</summary>
public static class CodigosErro
{
    public const string RequisicaoInvalida = "REQUISICAO_INVALIDA";
    public const string Validacao = "VALIDACAO";
    public const string ClienteNaoEncontrado = "CLIENTE_NAO_ENCONTRADO";
    public const string ProdutoNaoEncontrado = "PRODUTO_NAO_ENCONTRADO";
    public const string EstoqueInsuficiente = "ESTOQUE_INSUFICIENTE";
}
