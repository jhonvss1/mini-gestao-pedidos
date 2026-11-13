namespace Pedidos.Application.Comum;

/// <summary>Resultado único dos casos de uso: tipo + lista de erros (RN-11), sem biblioteca de notificação.</summary>
public class Resultado
{
    protected Resultado(TipoResultado tipo, IReadOnlyList<Erro> erros)
    {
        Tipo = tipo;
        Erros = erros;
    }

    public TipoResultado Tipo { get; }

    public IReadOnlyList<Erro> Erros { get; }

    public bool Sucesso => Tipo == TipoResultado.Sucesso;
}

public class Resultado<T> : Resultado
{
    private Resultado(TipoResultado tipo, IReadOnlyList<Erro> erros, T? valor)
        : base(tipo, erros)
    {
        Valor = valor;
    }

    /// <summary>Presente apenas quando <see cref="Resultado.Sucesso"/>.</summary>
    public T? Valor { get; }

    public static Resultado<T> Ok(T valor) => new(TipoResultado.Sucesso, [], valor);

    public static Resultado<T> RequisicaoInvalida(IReadOnlyList<Erro> erros) =>
        Falha(TipoResultado.RequisicaoInvalida, erros);

    public static Resultado<T> Validacao(IReadOnlyList<Erro> erros) => Falha(TipoResultado.Validacao, erros);

    public static Resultado<T> NaoEncontrado(IReadOnlyList<Erro> erros) => Falha(TipoResultado.NaoEncontrado, erros);

    public static Resultado<T> Conflito(IReadOnlyList<Erro> erros) => Falha(TipoResultado.Conflito, erros);

    private static Resultado<T> Falha(TipoResultado tipo, IReadOnlyList<Erro> erros)
    {
        if (erros.Count == 0)
            throw new ArgumentException("Uma falha precisa de ao menos um erro.", nameof(erros));

        return new(tipo, erros, default);
    }
}
