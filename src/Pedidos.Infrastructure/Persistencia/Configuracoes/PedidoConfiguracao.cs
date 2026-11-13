using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pedidos.Domain.Entidades;

namespace Pedidos.Infrastructure.Persistencia.Configuracoes;

public class PedidoConfiguracao : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> e)
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
    }
}
