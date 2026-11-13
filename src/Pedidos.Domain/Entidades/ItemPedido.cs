namespace Pedidos.Domain.Entidades;

public class ItemPedido
{
    internal ItemPedido(int produtoId, int quantidade, decimal precoUnitario)
    {
        ProdutoId = produtoId;
        Quantidade = quantidade;
        PrecoUnitario = precoUnitario;
    }

    public int Id { get; private set; }

    public int ProdutoId { get; private set; }

    public int Quantidade { get; private set; }

    /// <summary>Preço do produto no momento da criação, copiado do cadastro (RN-04).</summary>
    public decimal PrecoUnitario { get; private set; }

    public decimal Subtotal => Quantidade * PrecoUnitario;
}
