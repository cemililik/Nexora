using Microsoft.EntityFrameworkCore;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Infrastructure.Modules;

/// <summary>
/// EF Core context for the tenant-scoped demo-seed marker store (T-005). Lives
/// alongside <c>TenantConfigDbContext</c> rather than inside a module because
/// the marker table is cross-module bookkeeping; giving it to any one module
/// would create a false ownership claim.
/// </summary>
public sealed class DemoSeedMarkerDbContext(
    DbContextOptions<DemoSeedMarkerDbContext> options,
    ITenantContextAccessor tenantContextAccessor) : DbContext(options)
{
    public DbSet<DemoSeedMarker> Markers => Set<DemoSeedMarker>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Schema-per-tenant: markers are tenant-local so a dev/demo tenant reset
        // is a schema-drop, nothing cross-tenant.
        try
        {
            var schema = tenantContextAccessor.Current.SchemaName;
            if (!string.IsNullOrEmpty(schema))
                modelBuilder.HasDefaultSchema(schema);
        }
        catch (InvalidOperationException)
        {
            // Design-time / migration-gen — no tenant context. Fall through.
        }

        modelBuilder.Entity<DemoSeedMarker>(e =>
        {
            e.ToTable("platform_demo_seed_markers");
            e.HasKey(m => new { m.TenantId, m.ModuleName, m.Scenario });
            e.Property(m => m.ModuleName).HasMaxLength(100);
            e.Property(m => m.Scenario).HasMaxLength(50);
            e.Property(m => m.SeededAt).IsRequired();
        });
    }
}
