using Microsoft.EntityFrameworkCore;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Infrastructure.Configuration;

/// <summary>
/// EF Core context for tenant-specific key-value configuration stored in the tenant schema.
/// Hosts three related stores (per ADR-0025):
/// <list type="bullet">
///   <item><see cref="Configurations"/> — tenant-default values (<c>platform_tenant_config</c>).</item>
///   <item><see cref="OrgOverrides"/> — org-scope overrides (<c>platform_org_config</c>).</item>
///   <item><see cref="PolicyAudit"/> — append-only audit of every compliance-config write.</item>
/// </list>
/// </summary>
public sealed class TenantConfigDbContext(
    DbContextOptions<TenantConfigDbContext> options,
    ITenantContextAccessor tenantContextAccessor) : DbContext(options)
{
    public DbSet<TenantConfigEntry> Configurations => Set<TenantConfigEntry>();
    public DbSet<OrgConfigEntry> OrgOverrides => Set<OrgConfigEntry>();
    public DbSet<CompliancePolicyAuditEntry> PolicyAudit => Set<CompliancePolicyAuditEntry>();

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

        modelBuilder.Entity<OrgConfigEntry>(e =>
        {
            e.ToTable("platform_org_config");
            e.HasKey(c => new { c.OrganizationId, c.Key });
            e.Property(c => c.Key).HasMaxLength(256);
            e.Property(c => c.Value).HasColumnType("jsonb");
            e.Property(c => c.UpdatedBy).HasMaxLength(200);
        });

        modelBuilder.Entity<CompliancePolicyAuditEntry>(e =>
        {
            e.ToTable("platform_compliance_policy_audit");
            e.HasKey(c => c.Id);
            e.Property(c => c.Key).HasMaxLength(256).IsRequired();
            e.Property(c => c.OldValue).HasColumnType("jsonb");
            e.Property(c => c.NewValue).HasColumnType("jsonb");
            e.Property(c => c.Reason).HasMaxLength(500);
        });
    }
}
