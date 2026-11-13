using Pedidos.Application.Comum;
using Pedidos.Domain.ObjetosDeValor;

namespace Pedidos.Application.Validacao;

/// <summary>Resultado da etapa 1: todos os erros da etapa e, se não houver, os itens já consolidados (RN-09).</summary>
public sealed record ResultadoValidacao(IReadOnlyList<Erro> Erros, IReadOnlyList<ItemSolicitado> Itens)
{
    public bool Valido => Erros.Count == 0;
}
