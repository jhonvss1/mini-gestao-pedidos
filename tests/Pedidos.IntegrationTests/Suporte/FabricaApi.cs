using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Pedidos.IntegrationTests.Suporte;

/// <summary>API real em memória apontando para o banco de teste, com relógio fixo e interceptors opcionais.</summary>
public class FabricaApi(string stringConexao, params IInterceptor[] interceptors) : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset Agora = new(2026, 11, 13, 10, 30, 15, 789, TimeSpan.Zero);

    /// <summary>Ajustes extras de serviços, definidos antes do primeiro <c>CreateClient</c>.</summary>
    public Action<IServiceCollection>? ConfigurarServicos { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Pedidos", stringConexao);
        builder.ConfigureTestServices(servicos =>
        {
            servicos.RemoveAll<TimeProvider>();
            servicos.AddSingleton<TimeProvider>(new FakeTimeProvider(Agora));
            foreach (var interceptor in interceptors)
                servicos.AddSingleton(interceptor);
            ConfigurarServicos?.Invoke(servicos);
        });
    }
}
