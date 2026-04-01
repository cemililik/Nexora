using Microsoft.EntityFrameworkCore;

namespace Nexora.Infrastructure.Persistence.Outbox;

/// <summary>
/// Dedicated DbContext for the outbox_messages table in the public schema.
/// Deliberately does NOT inherit from BaseDbContext — outbox messages have no tenant scoping,
/// soft delete, or domain event dispatching. This keeps the outbox infrastructure decoupled
/// from the module-level persistence concerns.
/// </summary>
public sealed class OutboxDbContext(
    DbContextOptions<OutboxDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
