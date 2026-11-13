namespace Pedidos.Application.Comandos;

/// <summary>
/// Entrada do caso de uso. Campos anuláveis: ausente é erro de validação (422), não de formato (400).
/// Nenhum preço entra aqui: ele vem do cadastro (RN-04).
/// </summary>
public record CriarPedidoComando(int? ClienteId, IReadOnlyList<ItemComando?>? Itens);
