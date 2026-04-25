using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Infrastructure;

/// <summary>
/// Platform-level DbContext for tenant management.
/// Uses 'public' schema — NOT tenant-scoped.
/// Handles audit fields and soft delete conversion like BaseDbContext.
/// </summary>
public sealed class PlatformDbContext(
    DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantModule> TenantModules => Set<TenantModule>();
    public DbSet<PlatformLicenseCache> LicenseCache => Set<PlatformLicenseCache>();

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ConvertDeletesAndSetAuditFields();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ConvertDeletesAndSetAuditFields();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <inheritdoc />
    public override int SaveChanges()
    {
        ConvertDeletesAndSetAuditFields();
        return base.SaveChanges();
    }

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ConvertDeletesAndSetAuditFields();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>
    /// When true, the soft-delete conversion in
    /// <see cref="ConvertDeletesAndSetAuditFields"/> is skipped so a
    /// <c>Remove(entity)</c> physically deletes the row. Toggle via
    /// <see cref="EnterHardDeleteScope"/>; direct mutation is
    /// disallowed. Required by T-025 to actually expunge a
    /// <c>TenantModule</c> row whose retention window has elapsed —
    /// otherwise reinstall would trip the unique-on-not-deleted index.
    /// </summary>
    public bool HardDeleteEnabled { get; private set; }

    /// <summary>
    /// Disposes-back-to-soft-delete scope. Wrap the
    /// <c>Remove + SaveChangesAsync</c> in this and the row is
    /// physically deleted exactly once.
    /// </summary>
    public IDisposable EnterHardDeleteScope() => new HardDeleteScope(this);

    private sealed class HardDeleteScope : IDisposable
    {
        private readonly PlatformDbContext _ctx;
        private readonly bool _previous;
        public HardDeleteScope(PlatformDbContext ctx)
        {
            _ctx = ctx;
            _previous = ctx.HardDeleteEnabled;
            ctx.HardDeleteEnabled = true;
        }
        public void Dispose() => _ctx.HardDeleteEnabled = _previous;
    }

    private void ConvertDeletesAndSetAuditFields()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            // Convert hard deletes to soft deletes
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable && !HardDeleteEnabled)
            {
                entry.State = EntityState.Modified;
                entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = true;
                entry.Property(nameof(ISoftDeletable.DeletedAt)).CurrentValue = now;
                // DeletedBy is intentionally null for platform-level operations where user context
                // (ITenantContextAccessor) is not available. Platform operations are admin-initiated
                // and tracked via audit logs rather than per-entity DeletedBy fields.
                entry.Property(nameof(ISoftDeletable.DeletedBy)).CurrentValue = (string?)null;
                continue;
            }

            var hasCreatedAt = entry.Properties.Any(p => p.Metadata.Name == "CreatedAt");
            if (!hasCreatedAt) continue;

            if (entry.State == EntityState.Added)
                entry.Property("CreatedAt").CurrentValue = now;
            else if (entry.State == EntityState.Modified)
                entry.Property("UpdatedAt").CurrentValue = now;
        }
    }

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
            e.HasIndex(t => t.Slug).IsUnique().HasFilter("\"IsDeleted\" = false");
            // MaxLength matches TenantConfiguration.cs (50) — earlier
            // 20 here was inconsistent and would have rejected any future
            // status enum value longer than 20 chars (review round-2).
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(50);
            e.Property(t => t.Settings).HasColumnType("jsonb");
            e.Property(t => t.RealmId).HasMaxLength(200);

            // Audit fields
            e.Property("CreatedAt");
            e.Property("CreatedBy").HasMaxLength(200);
            e.Property("UpdatedAt");
            e.Property("UpdatedBy").HasMaxLength(200);

            // Soft delete fields
            e.Property("IsDeleted").HasDefaultValue(false);
            e.Property("DeletedAt");
            e.Property("DeletedBy").HasMaxLength(200);

            e.Ignore(t => t.Organizations);
            e.Ignore(t => t.Modules);

            e.HasQueryFilter(t => !t.IsDeleted);
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
            e.HasIndex(tm => new { tm.TenantId, tm.ModuleName }).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.Property(tm => tm.DeletedTableNames).HasMaxLength(4000);

            // Soft delete fields
            e.Property("IsDeleted").HasDefaultValue(false);
            e.Property("DeletedAt");
            e.Property("DeletedBy").HasMaxLength(200);

            // Audit fields
            e.Property("CreatedAt");
            e.Property("CreatedBy").HasMaxLength(200);
            e.Property("UpdatedAt");
            e.Property("UpdatedBy").HasMaxLength(200);

            e.HasQueryFilter(tm => !tm.IsDeleted);
        });

        modelBuilder.Entity<PlatformLicenseCache>(e =>
        {
            e.ToTable("platform_license_cache");
            // Composite PK: one row per (tenant, module)
            e.HasKey(lc => new { lc.TenantId, lc.ModuleName });
            e.Property(lc => lc.TenantId)
                .HasConversion(id => id.Value, v => TenantId.From(v))
                .IsRequired();
            e.Property(lc => lc.ModuleName).HasMaxLength(100).IsRequired();
            e.Property(lc => lc.IsLicensed).IsRequired();
            e.Property(lc => lc.CachedAt).IsRequired();
            e.Property(lc => lc.ExpiresAt).IsRequired();
        });
    }
}
