namespace Pedidos.Domain;

/// <summary>Limites numéricos do domínio (RN-24).</summary>
public static class Limites
{
    /// <summary>Maior quantidade (consolidada) aceita por produto: cabe em <c>int</c>, como quantidade e estoque.</summary>
    public const int QuantidadeMaxima = int.MaxValue;

    /// <summary>Maior valor monetário que cabe em <c>decimal(18,2)</c>.</summary>
    public const decimal ValorMonetarioMaximo = 9_999_999_999_999_999.99m;
}
