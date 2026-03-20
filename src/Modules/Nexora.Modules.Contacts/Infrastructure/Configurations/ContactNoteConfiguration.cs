using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Infrastructure.Configurations;

/// <summary>EF Core configuration for the ContactNote entity.</summary>
public sealed class ContactNoteConfiguration : IEntityTypeConfiguration<ContactNote>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ContactNote> builder)
    {
        builder.ToTable("contacts_notes");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasConversion(id => id.Value, v => ContactNoteId.From(v));
        builder.Property(n => n.ContactId).HasConversion(id => id.Value, v => ContactId.From(v));
        builder.Property(n => n.Content).IsRequired();

        builder.HasIndex(n => n.ContactId);
        builder.HasIndex(n => n.OrganizationId);
    }
}
