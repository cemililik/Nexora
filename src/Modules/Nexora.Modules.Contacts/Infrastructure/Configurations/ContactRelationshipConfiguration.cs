using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Infrastructure.Configurations;

/// <summary>EF Core configuration for the ContactRelationship entity.</summary>
public sealed class ContactRelationshipConfiguration : IEntityTypeConfiguration<ContactRelationship>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ContactRelationship> builder)
    {
        builder.ToTable("contacts_relationships");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, v => ContactRelationshipId.From(v));
        builder.Property(r => r.ContactId).HasConversion(id => id.Value, v => ContactId.From(v));
        builder.Property(r => r.RelatedContactId).HasConversion(id => id.Value, v => ContactId.From(v));
        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(r => r.ContactId);
        builder.HasIndex(r => r.RelatedContactId);
    }
}
