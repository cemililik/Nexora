using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Domain.ValueObjects;

namespace Nexora.Modules.Audit.Infrastructure.Configurations;

/// <summary>EF Core configuration for the AuditSetting entity.</summary>
public sealed class AuditSettingConfiguration : IEntityTypeConfiguration<AuditSetting>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditSetting> builder)
    {
        builder.ToTable("audit_settings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasConversion(id => id.Value, v => AuditSettingId.From(v));

        builder.Property(s => s.TenantId).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Module).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Operation).HasMaxLength(200).IsRequired();
        builder.Property(s => s.UpdatedByUser).HasMaxLength(320);

        builder.HasIndex(s => new { s.TenantId, s.Module, s.Operation })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}
