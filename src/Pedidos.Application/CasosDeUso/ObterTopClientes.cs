using Pedidos.Application.Comum;
using Pedidos.Application.Consultas;
using Pedidos.Application.Interfaces;
using Pedidos.Application.Modelos;

namespace Pedidos.Application.CasosDeUso;

public class ObterTopClientes(IPedidoConsultas consultas)
{
    public const int Quantidade = 5;

    public async Task<Resultado<IReadOnlyList<ClienteRanking>>> ExecutarAsync(
        ObterTopClientesConsulta consulta,
        CancellationToken cancellationToken)
    {
        var erros = new List<Erro>();
        if (consulta.DataInicio is null)
            erros.Add(Invalido("dataInicio", Mensagens.ParametroObrigatorio("dataInicio")));
        if (consulta.DataFim is null)
            erros.Add(Invalido("dataFim", Mensagens.ParametroObrigatorio("dataFim")));
        if (consulta.DataInicio > consulta.DataFim)
            erros.Add(Invalido("dataInicio", Mensagens.IntervaloInvalido));
        if (erros.Count > 0)
            return Resultado<IReadOnlyList<ClienteRanking>>.RequisicaoInvalida(erros);

        var ranking = await consultas.ObterTopClientesAsync(
            PeriodoDeDatas.Inicio(consulta.DataInicio)!.Value,
            PeriodoDeDatas.FimExclusivo(consulta.DataFim),
            Quantidade,
            cancellationToken);

        return Resultado<IReadOnlyList<ClienteRanking>>.Ok(ranking);
    }

    private static Erro Invalido(string campo, string mensagem) => new(CodigosErro.RequisicaoInvalida, mensagem, campo);
}
