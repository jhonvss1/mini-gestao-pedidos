namespace Pedidos.Domain;

public class Pedido
{
    private readonly List<ItemPedido> _itens = [];

    private Pedido(int clienteId, DateTime dataCriacao, IEnumerable<ItemPedido> itens)
    {
        ClienteId = clienteId;
        DataCriacao = dataCriacao;
        _itens.AddRange(itens);
        ValorTotal = _itens.Sum(i => i.Subtotal);
    }

    public int Id { get; private set; }

    public int ClienteId { get; private set; }

    /// <summary>UTC, truncada para segundos (a coluna é datetime2(0)).</summary>
    public DateTime DataCriacao { get; private set; }

    public decimal ValorTotal { get; private set; }

    public IReadOnlyCollection<ItemPedido> Itens => _itens;

    /// <summary>
    /// Decide a criação do pedido sobre um snapshot dos produtos (função pura, reexecutável). As etapas seguem a
    /// RN-11 a partir da existência dos produtos: existência, limites monetários e estoque; para na primeira com falhas.
    /// Não altera saldos: devolve as baixas que a persistência deve aplicar.
    /// </summary>
    /// <param name="itens">Itens já consolidados (um por produto), não vazios.</param>
    /// <param name="produtos">Snapshot lido na mesma transação; fornece preço e saldo.</param>
    public static ResultadoCriacaoPedido Criar(
        int clienteId,
        DateTime agora,
        IReadOnlyList<ItemSolicitado> itens,
        IEnumerable<Produto> produtos)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(clienteId);
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(produtos);
        if (agora.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A data de criação deve estar em UTC.", nameof(agora));
        if (itens.Count == 0)
            throw new ArgumentException("O pedido precisa de ao menos um item.", nameof(itens));
        if (itens.Select(i => i.ProdutoId).Distinct().Count() != itens.Count)
            throw new ArgumentException("Os itens devem estar consolidados: um por produto.", nameof(itens));

        var ordenados = itens.OrderBy(i => i.ProdutoId).ToList();
        var catalogo = produtos.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());

        var inexistentes = ordenados.Where(i => !catalogo.ContainsKey(i.ProdutoId)).Select(i => i.ProdutoId).ToList();
        if (inexistentes.Count > 0)
            return new ResultadoCriacaoPedido.ProdutosInexistentes(inexistentes);

        var limite = VerificarLimitesMonetarios(ordenados, catalogo);
        if (limite is not null)
            return limite;

        var faltas = ordenados
            .Where(i => !catalogo[i.ProdutoId].TemEstoque(i.Quantidade))
            .Select(i => new FaltaDeEstoque(i.ProdutoId, i.Quantidade, catalogo[i.ProdutoId].Estoque))
            .ToList();
        if (faltas.Count > 0)
            return new ResultadoCriacaoPedido.EstoqueInsuficiente(faltas);

        var itensPedido = ordenados.Select(i => new ItemPedido(i.ProdutoId, i.Quantidade, catalogo[i.ProdutoId].Preco));
        var pedido = new Pedido(clienteId, TruncarParaSegundos(agora), itensPedido);
        var baixas = ordenados.Select(i => new BaixaEstoque(i.ProdutoId, i.Quantidade)).ToList();

        return new ResultadoCriacaoPedido.Criado(pedido, baixas);
    }

    private static ResultadoCriacaoPedido.LimiteExcedido? VerificarLimitesMonetarios(
        List<ItemSolicitado> itens,
        Dictionary<int, Produto> catalogo)
    {
        var subtotais = new List<decimal>();
        var excedidos = new List<int>();

        foreach (var item in itens)
        {
            var subtotal = Multiplicar(item.Quantidade, catalogo[item.ProdutoId].Preco);
            if (subtotal is null or > Limites.ValorMonetarioMaximo)
                excedidos.Add(item.ProdutoId);
            else
                subtotais.Add(subtotal.Value);
        }

        if (excedidos.Count > 0)
            return new ResultadoCriacaoPedido.LimiteExcedido(excedidos, TotalExcedido: false);

        var total = Somar(subtotais);
        return total is null or > Limites.ValorMonetarioMaximo
            ? new ResultadoCriacaoPedido.LimiteExcedido([], TotalExcedido: true)
            : null;
    }

    // Aritmética decimal lança OverflowException acima de ~7,9e28; aqui isso também é "limite excedido".
    private static decimal? Multiplicar(int quantidade, decimal preco)
    {
        try
        {
            return quantidade * preco;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static decimal? Somar(List<decimal> valores)
    {
        try
        {
            return valores.Sum();
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static DateTime TruncarParaSegundos(DateTime data) =>
        new(data.Ticks - data.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
}
