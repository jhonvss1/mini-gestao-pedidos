using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Pedidos.Application.Interfaces;
using Pedidos.Infrastructure.Persistencia;

namespace Pedidos.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra o DbContext e as portas de Application. Qualquer <see cref="IInterceptor"/> registrado no contêiner é
    /// aplicado ao contexto: é o ponto de extensão usado pelos testes para injetar falhas. Não há
    /// EnableRetryOnFailure: a transação é explícita e o retry de deadlock fica no repositório.
    /// </summary>
    public static IServiceCollection AddInfraestrutura(this IServiceCollection services, string stringConexao)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stringConexao);

        services.AddDbContext<PedidosDbContext>((provedor, opcoes) => opcoes
            .UseSqlServer(stringConexao)
            .AddInterceptors(provedor.GetServices<IInterceptor>()));
        services.AddScoped<IPedidoRepositorio, PedidoRepositorio>();
        services.AddScoped<IPedidoConsultas, PedidoConsultas>();

        return services;
    }
}
