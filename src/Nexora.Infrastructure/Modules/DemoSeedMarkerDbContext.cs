using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Persistence;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Infrastructure.Modules;

/// <summary>
/// EF Core context for the tenant-scoped demo-seed marker store (T-005). Lives
/// alongside <c>TenantConfigDbContext</c> rather than inside a module because
/// the marker table is cross-module bookkeeping; giving it to any one module
/// would create a false ownership claim.
///
/// <para>
/// Inherits <see cref="BaseDbContext"/> so the
/// <c>TenantModelCacheKeyFactory</c> distinguishes this context's model per
/// tenant schema — without that, the first tenant's resolved schema is cached
/// and reused for every subsequent tenant, which would silently mis-route
/// reads / writes in the production-like Postgres path.
/// </para>
/// </summary>
public sealed class DemoSeedMarkerDbContext(
    DbContextOptions<DemoSeedMarkerDbContext> options,
    ITenantContextAccessor tenantContextAccessor)
    : BaseDbContext(options, tenantContextAccessor)
{
    /// <summary>
    /// Tenant-local marker rows recording per-(tenant, module, scenario)
    /// demo-seed lifecycle (see <see cref="DemoSeedMarker"/>). Composite key
    /// is (TenantId, ModuleName, Scenario).
    /// </summary>
    public DbSet<DemoSeedMarker> Markers => Set<DemoSeedMarker>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // BaseDbContext.OnModelCreating sets HasDefaultSchema(tenantSchema)
        // when a tenant context is available; we just register the entity.
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DemoSeedMarker>(e =>
        {
            // Table name dropped the historical "platform_" prefix: the row
            // lives in the tenant schema (BaseDbContext sets HasDefaultSchema
            // to the tenant), so the prefix was misleading. Rename is safe
            // because the table just shipped (T-005) and no production
            // tenant has been provisioned with the old name yet.
            e.ToTable("demo_seed_markers");
            e.HasKey(m => new { m.TenantId, m.ModuleName, m.Scenario });
            e.Property(m => m.ModuleName).HasMaxLength(100);
            e.Property(m => m.Scenario).HasMaxLength(50);
            e.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(m => m.StartedAt).IsRequired();
            e.Property(m => m.CompletedAt); // nullable
        });
    }
}
