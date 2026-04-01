using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Persistence.Outbox;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Infrastructure.Tests.Persistence.Outbox;

/// <summary>Test DbContext with OutboxMessage mapped, used by OutboxServiceTests.</summary>
public sealed class OutboxTestDbContext(DbContextOptions<OutboxTestDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}

/// <summary>Integration event used by OutboxServiceTests.</summary>
internal sealed record TestIntegrationEvent : IntegrationEventBase
{
    public string Data { get; init; } = "test";
}

/// <summary>
/// Integration event used by OutboxProcessorTests.
/// Must be public so AssemblyQualifiedName resolves via Type.GetType during outbox processing.
/// </summary>
public sealed record TestProcessorEvent : IntegrationEventBase
{
    public string Data { get; init; } = "test";
}
