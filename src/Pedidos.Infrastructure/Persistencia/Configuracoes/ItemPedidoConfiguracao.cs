using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pedidos.Domain.Entidades;

namespace Pedidos.Infrastructure.Persistencia.Configuracoes;

public class ItemPedidoConfiguracao : IEntityTypeConfiguration<ItemPedido>
{
    public void Configure(EntityTypeBuilder<ItemPedido> e)
    {
        e.ToTable("item_pedido");
        e.HasKey(i => i.Id);
        e.Property(i => i.Id).HasColumnName("id");
        e.Property<int>("PedidoId").HasColumnName("pedido_id");
        e.Property(i => i.ProdutoId).HasColumnName("produto_id");
        e.Property(i => i.Quantidade).HasColumnName("quantidade");
        e.Property(i => i.PrecoUnitario).HasColumnName("preco_unitario").HasPrecision(18, 2);
        e.Ignore(i => i.Subtotal);
    }
}
