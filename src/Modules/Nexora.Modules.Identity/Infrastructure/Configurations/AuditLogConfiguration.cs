using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;

namespace Nexora.Modules.Identity.Infrastructure.Configurations;

/// <summary>EF Core configuration for identity_audit_logs table.</summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("identity_audit_logs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, v => AuditLogId.From(v));
        builder.Property(a => a.UserId).HasConversion(id => id.Value, v => UserId.From(v));
        builder.Property(a => a.TenantId).HasConversion(id => id.Value, v => TenantId.From(v));

        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.IpAddress).HasMaxLength(45);
        builder.Property(a => a.UserAgent).HasMaxLength(500);
        builder.Property(a => a.Details).HasColumnType("jsonb");

        builder.HasIndex(a => a.TenantId);
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.Timestamp);
    }
}
