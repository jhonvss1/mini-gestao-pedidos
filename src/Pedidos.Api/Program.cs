using Pedidos.Api.Erros;
using Pedidos.Application.CasosDeUso;
using Pedidos.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var stringConexao = builder.Configuration.GetConnectionString("Pedidos")
    ?? throw new InvalidOperationException("Configure a string de conexão 'ConnectionStrings:Pedidos'.");

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(opcoes =>
        opcoes.InvalidModelStateResponseFactory = contexto => RespostasDeErro.DeModelStateInvalido(contexto.ModelState));

builder.Services.AddExceptionHandler<ManipuladorDeExcecoes>();
builder.Services.AddProblemDetails();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CriarPedido>();
builder.Services.AddScoped<ListarPedidos>();
builder.Services.AddScoped<ObterTopClientes>();
builder.Services.AddScoped<ListarCatalogo>();
builder.Services.AddInfraestrutura(stringConexao);

var app = builder.Build();

app.UseExceptionHandler();
app.MapControllers();

app.Run();

public partial class Program;
