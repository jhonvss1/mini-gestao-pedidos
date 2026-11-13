using Microsoft.EntityFrameworkCore;
using Pedidos.Domain;

namespace Pedidos.Infrastructure.Persistencia;

/// <summary>
/// Mapeamento explícito para o schema de database/ (fonte do schema: sem migrations e sem EnsureCreated).
/// C# em PascalCase; tabelas no singular e colunas em snake_case, traduzidas aqui.
/// </summary>
public class PedidosDbContext(DbContextOptions<PedidosDbContext> options) : DbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<Produto> Produtos => Set<Produto>();

    public DbSet<Pedido> Pedidos => Set<Pedido>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cliente>(e =>
        {
            e.ToTable("cliente");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("id");
            e.Property(c => c.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Produto>(e =>
        {
            e.ToTable("produto");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnName("id");
            e.Property(p => p.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
            e.Property(p => p.Preco).HasColumnName("preco").HasPrecision(18, 2);
            e.Property(p => p.Estoque).HasColumnName("estoque");
        });

        modelBuilder.Entity<Pedido>(e =>
        {
            e.ToTable("pedido");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnName("id");
            e.Property(p => p.ClienteId).HasColumnName("cliente_id");
            e.Property(p => p.DataCriacao)
                .HasColumnName("data_criacao")
                .HasColumnType("datetime2(0)")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            e.Property(p => p.ValorTotal).HasColumnName("valor_total").HasPrecision(18, 2);

            // Itens pelo campo de apoio (_itens); a FK pedido_id é propriedade de sombra.
            e.HasMany(p => p.Itens).WithOne().HasForeignKey("PedidoId").IsRequired();
            e.Navigation(p => p.Itens).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ItemPedido>(e =>
        {
            e.ToTable("item_pedido");
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).HasColumnName("id");
            e.Property<int>("PedidoId").HasColumnName("pedido_id");
            e.Property(i => i.ProdutoId).HasColumnName("produto_id");
            e.Property(i => i.Quantidade).HasColumnName("quantidade");
            e.Property(i => i.PrecoUnitario).HasColumnName("preco_unitario").HasPrecision(18, 2);
            e.Ignore(i => i.Subtotal);
        });
    }
}
