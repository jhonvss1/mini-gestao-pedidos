using Pedidos.Application.Comum;
using Pedidos.Application.Consultas;
using Pedidos.Application.Interfaces;
using Pedidos.Application.Modelos;

namespace Pedidos.Application.CasosDeUso;

public class ListarPedidos(IPedidoConsultas consultas)
{
    public const int PaginaPadrao = 1;
    public const int TamanhoPaginaPadrao = 20;
    public const int TamanhoPaginaMaximo = 100;

    public async Task<Resultado<PaginaDePedidos>> ExecutarAsync(
        ListarPedidosConsulta consulta,
        CancellationToken cancellationToken)
    {
        var pagina = consulta.Pagina ?? PaginaPadrao;
        var tamanho = consulta.TamanhoPagina ?? TamanhoPaginaPadrao;

        var erros = new List<Erro>();
        if (pagina < 1)
            erros.Add(Invalido("pagina", Mensagens.PaginaInvalida));
        if (tamanho is < 1 or > TamanhoPaginaMaximo)
            erros.Add(Invalido("tamanhoPagina", Mensagens.TamanhoPaginaInvalido(TamanhoPaginaMaximo)));
        if (consulta.DataInicio > consulta.DataFim)
            erros.Add(Invalido("dataInicio", Mensagens.IntervaloInvalido));
        if (erros.Count > 0)
            return Resultado<PaginaDePedidos>.RequisicaoInvalida(erros);

        var filtro = new FiltroPedidos(
            consulta.ClienteId,
            Inicio: PeriodoDeDatas.Inicio(consulta.DataInicio),
            FimExclusivo: PeriodoDeDatas.FimExclusivo(consulta.DataFim),
            pagina,
            tamanho);

        var encontrados = await consultas.ListarAsync(filtro, cancellationToken);
        var totalPaginas = (int)Math.Ceiling(encontrados.TotalItens / (double)tamanho);

        return Resultado<PaginaDePedidos>.Ok(
            new PaginaDePedidos(encontrados.Registros, pagina, tamanho, encontrados.TotalItens, totalPaginas));
    }

    private static Erro Invalido(string campo, string mensagem) => new(CodigosErro.RequisicaoInvalida, mensagem, campo);
}
