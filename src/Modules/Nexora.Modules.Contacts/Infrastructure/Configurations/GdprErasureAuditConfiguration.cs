using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Contacts.Domain.Entities;

namespace Nexora.Modules.Contacts.Infrastructure.Configurations;

/// <summary>EF Core configuration for the <see cref="GdprErasureAudit"/> entity.</summary>
public sealed class GdprErasureAuditConfiguration : IEntityTypeConfiguration<GdprErasureAudit>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GdprErasureAudit> builder)
    {
        builder.ToTable("contacts_gdpr_erasure_audit");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.ContactId).IsRequired();
        builder.Property(a => a.ErasedByUserId).IsRequired();
        builder.Property(a => a.ErasedAtUtc).IsRequired();
        builder.Property(a => a.Reason).HasMaxLength(500).IsRequired();
        builder.Property(a => a.Mode).HasMaxLength(20).IsRequired();
        // Stored as text to remain portable across providers (PostgreSQL + SQLite for tests).
        // Contents are JSON; jsonb indexing is not needed for this audit table.
        builder.Property(a => a.ChildCountsJson).IsRequired();

        builder.HasIndex(a => new { a.TenantId, a.ErasedAtUtc });
        builder.HasIndex(a => a.ContactId);
    }
}
