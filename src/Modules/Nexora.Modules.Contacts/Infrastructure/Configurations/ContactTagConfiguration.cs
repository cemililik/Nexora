using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Infrastructure.Configurations;

/// <summary>EF Core configuration for the ContactTag entity.</summary>
public sealed class ContactTagConfiguration : IEntityTypeConfiguration<ContactTag>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ContactTag> builder)
    {
        builder.ToTable("contacts_contact_tags");
        builder.HasKey(ct => ct.Id);
        builder.Property(ct => ct.Id).HasConversion(id => id.Value, v => ContactTagId.From(v));
        builder.Property(ct => ct.ContactId).HasConversion(id => id.Value, v => ContactId.From(v));
        builder.Property(ct => ct.TagId).HasConversion(id => id.Value, v => TagId.From(v));

        // T-021: no HasFilter — ContactTag extends Entity<T>, not AuditableEntity<T>,
        // so the "IsDeleted" column does not exist on contacts_contact_tags. The
        // filter was dead code silently dropped by Postgres at CREATE INDEX time on
        // a fresh schema and is not needed semantically: rows are hard-deleted
        // directly when a contact/tag link is removed.
        builder.HasIndex(ct => new { ct.ContactId, ct.TagId, ct.OrganizationId }).IsUnique();
    }
}
