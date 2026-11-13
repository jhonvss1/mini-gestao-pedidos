namespace Pedidos.Application.Comum;

/// <summary>
/// Erro de negócio acumulável. <see cref="Dados"/> carrega os valores estruturados do erro
/// (ex.: produtoId, solicitado, disponivel) para a Api montar o contrato HTTP.
/// </summary>
public record Erro(
    string Codigo,
    string Mensagem,
    string? Campo = null,
    IReadOnlyDictionary<string, object>? Dados = null);
