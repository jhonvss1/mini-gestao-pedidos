namespace Pedidos.Application;

/// <summary>
/// Entrada do caso de uso. Campos anuláveis: ausente é erro de validação (422), não de formato (400).
/// Nenhum preço entra aqui: ele vem do cadastro (RN-04).
/// </summary>
public sealed record CriarPedidoComando(int? ClienteId, IReadOnlyList<ItemComando?>? Itens);

public sealed record ItemComando(int? ProdutoId, int? Quantidade);
