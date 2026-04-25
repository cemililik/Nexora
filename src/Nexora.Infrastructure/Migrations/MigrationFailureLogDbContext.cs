using Microsoft.EntityFrameworkCore;

namespace Nexora.Infrastructure.Migrations;

/// <summary>
/// Platform-scoped (NOT tenant-scoped) DbContext for the
/// <c>platform_migration_failures</c> table. Deliberately does NOT
/// inherit <c>BaseDbContext</c> — a migration-failure record about
/// tenant X must persist even if X's schema is unreachable / quarantined,
/// which means the DbContext lives in the <c>public</c> schema and does
/// not flip its <c>HasDefaultSchema</c> on the ambient tenant accessor.
/// Same shape as <c>OutboxDbContext</c>.
/// </summary>
public sealed class MigrationFailureLogDbContext(
    DbContextOptions<MigrationFailureLogDbContext> options) : DbContext(options)
{
    /// <summary>Failure rows — append-only forensic log.</summary>
    public DbSet<MigrationFailure> Failures => Set<MigrationFailure>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");

        modelBuilder.Entity<MigrationFailure>(e =>
        {
            e.ToTable("platform_migration_failures");
            e.HasKey(f => f.Id);
            e.Property(f => f.TenantId).IsRequired();
            e.Property(f => f.ModuleName).HasMaxLength(100).IsRequired();
            e.Property(f => f.ExceptionType).HasMaxLength(500).IsRequired();
            e.Property(f => f.ExceptionMessage).HasMaxLength(4000).IsRequired();
            e.Property(f => f.StackTrace).HasMaxLength(8000);
            e.Property(f => f.OccurredAtUtc).IsRequired();
            // Hot lookup pattern from the runbook: "SELECT … WHERE TenantId = X
            // ORDER BY OccurredAtUtc DESC LIMIT N" during ops triage.
            e.HasIndex(f => new { f.TenantId, f.OccurredAtUtc });
        });
    }
}
