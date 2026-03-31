using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Persistence.Outbox;

namespace Nexora.Infrastructure.Tests.Persistence.Outbox;

/// <summary>Test DbContext with OutboxMessage mapped.</summary>
public sealed class OutboxTestDbContext(DbContextOptions<OutboxTestDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
