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

        builder.HasIndex(cf => new { cf.ContactId, cf.FieldDefinitionId }).IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}
