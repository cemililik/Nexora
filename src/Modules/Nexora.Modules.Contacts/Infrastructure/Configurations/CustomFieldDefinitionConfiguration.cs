using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Infrastructure.Configurations;

/// <summary>EF Core configuration for the CustomFieldDefinition entity.</summary>
public sealed class CustomFieldDefinitionConfiguration : IEntityTypeConfiguration<CustomFieldDefinition>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> builder)
    {
        builder.ToTable("contacts_custom_field_definitions");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasConversion(id => id.Value, v => CustomFieldDefinitionId.From(v));

        builder.Property(d => d.FieldName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.FieldType).HasMaxLength(50).IsRequired();
        builder.Property(d => d.Options).HasColumnType("jsonb");

        builder.HasIndex(d => new { d.TenantId, d.FieldName }).IsUnique();
    }
}
