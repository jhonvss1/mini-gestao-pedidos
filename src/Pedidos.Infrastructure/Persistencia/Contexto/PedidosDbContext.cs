using Microsoft.EntityFrameworkCore;
using Pedidos.Domain.Entidades;

namespace Pedidos.Infrastructure.Persistencia.Contexto;

/// <summary>
/// Contexto do EF Core. O mapeamento é explícito e fica em <c>Configuracoes/</c>, uma classe por entidade: o schema
/// vem dos scripts de database/ (sem migrations e sem EnsureCreated), com tabelas no singular e colunas em snake_case.
/// </summary>
public class PedidosDbContext(DbContextOptions<PedidosDbContext> options) : DbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<Produto> Produtos => Set<Produto>();

    public DbSet<Pedido> Pedidos => Set<Pedido>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PedidosDbContext).Assembly);
}
