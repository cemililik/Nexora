using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Domain.Entities;

namespace Nexora.Modules.Identity.Infrastructure;

/// <summary>
/// Platform-level DbContext for tenant management.
/// Uses 'public' schema — NOT tenant-scoped.
/// Only contains: Tenants, TenantModules (platform-wide tables).
/// </summary>
public sealed class PlatformDbContext(
    DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantModule> TenantModules => Set<TenantModule>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");

        modelBuilder.Entity<Tenant>(e =>
        {
            e.ToTable("platform_tenants");
            e.HasKey(t => t.Id);
            e.Property(t => t.Id)
                .HasConversion(id => id.Value, v => Domain.ValueObjects.TenantId.From(v));
            e.Property(t => t.Name).HasMaxLength(200).IsRequired();
            e.Property(t => t.Slug).HasMaxLength(100).IsRequired();
            e.HasIndex(t => t.Slug).IsUnique();
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(t => t.Settings).HasColumnType("jsonb");
            e.Property(t => t.RealmId).HasMaxLength(200);

            // Audit fields
            e.Property("CreatedAt");
            e.Property("CreatedBy").HasMaxLength(200);
            e.Property("UpdatedAt");
            e.Property("UpdatedBy").HasMaxLength(200);

            e.Ignore(t => t.Organizations);
            e.Ignore(t => t.Modules);
        });

        modelBuilder.Entity<TenantModule>(e =>
        {
            e.ToTable("platform_tenant_modules");
            e.HasKey(tm => tm.Id);
            e.Property(tm => tm.Id)
                .HasConversion(id => id.Value, v => Domain.ValueObjects.TenantModuleId.From(v));
            e.Property(tm => tm.TenantId)
                .HasConversion(id => id.Value, v => Domain.ValueObjects.TenantId.From(v));
            e.Property(tm => tm.ModuleName).HasMaxLength(100).IsRequired();
            e.HasIndex(tm => new { tm.TenantId, tm.ModuleName }).IsUnique();
        });
    }
}
