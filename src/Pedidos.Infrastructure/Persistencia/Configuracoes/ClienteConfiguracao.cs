using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pedidos.Domain.Entidades;

namespace Pedidos.Infrastructure.Persistencia.Configuracoes;

public sealed class ClienteConfiguracao : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> e)
    {
        e.ToTable("cliente");
        e.HasKey(c => c.Id);
        e.Property(c => c.Id).HasColumnName("id");
        e.Property(c => c.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
    }
}
