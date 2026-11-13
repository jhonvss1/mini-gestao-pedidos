using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Pedidos.IntegrationTests.Suporte;

/// <summary>Simula uma falha na gravação do pedido, depois dos débitos de estoque e antes do commit.</summary>
public sealed class FalhaAoSalvarInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Falha injetada na gravação.");
}
