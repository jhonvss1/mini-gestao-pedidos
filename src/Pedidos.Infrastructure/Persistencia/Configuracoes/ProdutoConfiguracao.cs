using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pedidos.Domain.Entidades;

namespace Pedidos.Infrastructure.Persistencia.Configuracoes;

public sealed class ProdutoConfiguracao : IEntityTypeConfiguration<Produto>
{
    public void Configure(EntityTypeBuilder<Produto> e)
    {
        e.ToTable("produto");
        e.HasKey(p => p.Id);
        e.Property(p => p.Id).HasColumnName("id");
        e.Property(p => p.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        e.Property(p => p.Preco).HasColumnName("preco").HasPrecision(18, 2);
        e.Property(p => p.Estoque).HasColumnName("estoque");
    }
}
