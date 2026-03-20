using Microsoft.EntityFrameworkCore;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Infrastructure.Configuration;

/// <summary>
/// EF Core context for tenant-specific key-value configuration stored in the tenant schema.
/// </summary>
public sealed class TenantConfigDbContext(
    DbContextOptions<TenantConfigDbContext> options,
    ITenantContextAccessor tenantContextAccessor) : DbContext(options)
{
    public DbSet<TenantConfigEntry> Configurations => Set<TenantConfigEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var schema = tenantContextAccessor.Current.SchemaName;
        modelBuilder.HasDefaultSchema(schema);

        modelBuilder.Entity<TenantConfigEntry>(e =>
        {
            e.ToTable("platform_tenant_config");
            e.HasKey(c => c.Key);
            e.Property(c => c.Key).HasMaxLength(256);
            e.Property(c => c.Value).HasColumnType("jsonb");
        });
    }
}
