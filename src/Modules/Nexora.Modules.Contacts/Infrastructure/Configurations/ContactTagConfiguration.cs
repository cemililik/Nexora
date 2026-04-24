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

        // T-021: no HasFilter. ContactTag extends Entity<T>, NOT
        // AuditableEntity<T>, so the "IsDeleted" column is never created on
        // contacts_contact_tags. A filter referencing that column would not be
        // silently dropped — it would fail CREATE INDEX with 42703 "column
        // does not exist" against a fresh Postgres (the dev DB only worked
        // because its index pre-dated the filter). The filter was therefore
        // both broken and unnecessary: ContactTag rows are hard-deleted when
        // a contact/tag link is removed, so a plain unique index is correct.
        builder.HasIndex(ct => new { ct.ContactId, ct.TagId, ct.OrganizationId }).IsUnique();
    }
}
