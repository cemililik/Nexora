using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Domain.ValueObjects;

namespace Nexora.Modules.Audit.Infrastructure.Configurations;

/// <summary>EF Core configuration for the AuditEntry entity.</summary>
public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion(id => id.Value, v => AuditEntryId.From(v));

        builder.Property(e => e.TenantId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Module).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Operation).HasMaxLength(200).IsRequired();
        builder.Property(e => e.OperationType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.UserEmail).HasMaxLength(320);
        builder.Property(e => e.IpAddress).HasMaxLength(45);
        builder.Property(e => e.UserAgent).HasMaxLength(500);
        builder.Property(e => e.CorrelationId).HasMaxLength(100);
        builder.Property(e => e.ErrorKey).HasMaxLength(500);
        builder.Property(e => e.EntityType).HasMaxLength(200);
        builder.Property(e => e.EntityId).HasMaxLength(200);

        builder.Property(e => e.BeforeState).HasColumnType("jsonb");
        builder.Property(e => e.AfterState).HasColumnType("jsonb");
        builder.Property(e => e.Changes).HasColumnType("jsonb");
        builder.Property(e => e.Metadata).HasColumnType("jsonb");

        builder.HasIndex(e => new { e.TenantId, e.Timestamp })
            .IsDescending(false, true);
        builder.HasIndex(e => new { e.TenantId, e.Module });
        builder.HasIndex(e => new { e.TenantId, e.UserId });
        builder.HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId });
    }
}
