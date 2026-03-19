using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Infrastructure.Configurations;

public sealed class ContactTagConfiguration : IEntityTypeConfiguration<ContactTag>
{
    public void Configure(EntityTypeBuilder<ContactTag> builder)
    {
        builder.ToTable("contacts_contact_tags");
        builder.HasKey(ct => ct.Id);
        builder.Property(ct => ct.Id).HasConversion(id => id.Value, v => ContactTagId.From(v));
        builder.Property(ct => ct.ContactId).HasConversion(id => id.Value, v => ContactId.From(v));
        builder.Property(ct => ct.TagId).HasConversion(id => id.Value, v => TagId.From(v));

        builder.HasIndex(ct => new { ct.ContactId, ct.TagId, ct.OrganizationId }).IsUnique();
    }
}
