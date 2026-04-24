using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Infrastructure.Configurations;

/// <summary>EF Core configuration for the ContactCustomField entity.</summary>
public sealed class ContactCustomFieldConfiguration : IEntityTypeConfiguration<ContactCustomField>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ContactCustomField> builder)
    {
        builder.ToTable("contacts_custom_field_values");
        builder.HasKey(cf => cf.Id);
        builder.Property(cf => cf.Id).HasConversion(id => id.Value, v => ContactCustomFieldId.From(v));
        builder.Property(cf => cf.ContactId).HasConversion(id => id.Value, v => ContactId.From(v));
        builder.Property(cf => cf.FieldDefinitionId).HasConversion(id => id.Value, v => CustomFieldDefinitionId.From(v));
        builder.Property(cf => cf.Value).HasMaxLength(1000);

        // T-021: no HasFilter. ContactCustomField extends Entity<T>, NOT
        // AuditableEntity<T>, so the "IsDeleted" column is never created on
        // contacts_custom_field_values. A filter referencing that column would
        // not be silently dropped — it would fail CREATE INDEX with 42703
        // "column does not exist" against a fresh Postgres (the dev DB only
        // worked because its index was created before the filter clause was
        // added). The filter was therefore both broken and unnecessary:
        // custom-field values are hard-deleted when a contact's assignment
        // changes, so a plain unique index is exactly correct.
        builder.HasIndex(cf => new { cf.ContactId, cf.FieldDefinitionId }).IsUnique();
    }
}
