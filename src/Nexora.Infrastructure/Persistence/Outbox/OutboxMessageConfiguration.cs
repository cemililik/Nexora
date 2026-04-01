using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nexora.Infrastructure.Persistence.Outbox;

/// <summary>
/// EF Core configuration for the <see cref="OutboxMessage"/> entity.
/// Maps to the <c>outbox_messages</c> table in the DbContext's default schema (tenant schema).
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.EventType)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(m => m.EventPayload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(m => m.TenantId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        builder.Property(m => m.Error)
            .HasColumnType("text");

        builder.Property(m => m.RetryCount)
            .HasDefaultValue(0)
            .IsRequired();

        // Pending messages index — covers the OutboxProcessor polling query
        builder.HasIndex(m => m.CreatedAt)
            .HasDatabaseName("ix_outbox_pending")
            .HasFilter("\"ProcessedAt\" IS NULL");

        // Tenant + time index — covers tenant-scoped queries on pending messages
        builder.HasIndex(m => new { m.TenantId, m.CreatedAt })
            .HasDatabaseName("ix_outbox_tenant")
            .HasFilter("\"ProcessedAt\" IS NULL");
    }
}
