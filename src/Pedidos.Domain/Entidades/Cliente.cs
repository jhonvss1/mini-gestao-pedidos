namespace Pedidos.Domain.Entidades;

public class Cliente
{
    public Cliente(int id, string nome)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);

        Id = id;
        Nome = nome;
    }

    public int Id { get; private set; }

    public string Nome { get; private set; }
}
