using Microsoft.EntityFrameworkCore;

namespace Nexora.Infrastructure.Migrations;

/// <summary>
/// Platform-scoped (NOT tenant-scoped) DbContext for the
/// <c>platform_migration_drift</c> table. Same shape as
/// <see cref="MigrationFailureLogDbContext"/> — lives in the <c>public</c>
/// schema so drift recording works even when individual tenant schemas
/// are mid-migration or quarantined.
/// </summary>
public sealed class MigrationDriftLogDbContext(
    DbContextOptions<MigrationDriftLogDbContext> options) : DbContext(options)
{
    /// <summary>Drift rows — append-only forensic log.</summary>
    public DbSet<MigrationDrift> Drifts => Set<MigrationDrift>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");

        modelBuilder.Entity<MigrationDrift>(e =>
        {
            e.ToTable("platform_migration_drift", schema: "public");
            e.HasKey(d => d.Id);
            e.Property(d => d.TenantId).IsRequired();
            e.Property(d => d.ModuleName).HasMaxLength(100).IsRequired();
            // Migration IDs follow EF Core's `<timestamp>_<name>` convention; 200
            // chars covers any realistic name length (typical ≈70).
            e.Property(d => d.KnownHead).HasMaxLength(200);
            e.Property(d => d.AppliedHead).HasMaxLength(200);
            e.Property(d => d.DetectedAtUtc).IsRequired();
            // Hot lookup pattern: "SELECT * FROM platform_migration_drift
            //   WHERE TenantId = X ORDER BY DetectedAtUtc DESC LIMIT N"
            // mirrors the failure-log access pattern.
            e.HasIndex(d => new { d.TenantId, d.DetectedAtUtc });
        });
    }
}
