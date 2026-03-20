using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;

namespace Nexora.Modules.Identity.Infrastructure.Configurations;

/// <summary>EF Core configuration for the TenantModule entity.</summary>
public sealed class TenantModuleConfiguration : IEntityTypeConfiguration<TenantModule>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TenantModule> builder)
    {
        builder.ToTable("identity_tenant_modules");
        builder.HasKey(tm => tm.Id);
        builder.Property(tm => tm.Id).HasConversion(id => id.Value, v => TenantModuleId.From(v));
        builder.Property(tm => tm.TenantId).HasConversion(id => id.Value, v => TenantId.From(v));
        builder.Property(tm => tm.ModuleName).HasMaxLength(50).IsRequired();
        builder.HasIndex(tm => new { tm.TenantId, tm.ModuleName }).IsUnique();
        builder.Property(tm => tm.InstalledBy).HasMaxLength(200);
    }
}
