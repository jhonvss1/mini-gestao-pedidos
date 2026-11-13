namespace Pedidos.Domain.Entidades;

public class Produto
{
    public Produto(int id, string nome, decimal preco, int estoque)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(preco);
        ArgumentOutOfRangeException.ThrowIfNegative(estoque);

        Id = id;
        Nome = nome;
        Preco = preco;
        Estoque = estoque;
    }

    public int Id { get; private set; }

    public string Nome { get; private set; }

    /// <summary>Preço atual do cadastro. O pedido copia este valor para o item (RN-04).</summary>
    public decimal Preco { get; private set; }

    public int Estoque { get; private set; }

    /// <summary>Indica se o saldo atende a quantidade pedida (decisão sobre o snapshot lido; não altera o saldo).</summary>
    public bool TemEstoque(int quantidade)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantidade);
        return Estoque >= quantidade;
    }
}
