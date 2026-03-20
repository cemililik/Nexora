using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Infrastructure.Configurations;

/// <summary>EF Core configuration for the ContactAddress entity.</summary>
public sealed class ContactAddressConfiguration : IEntityTypeConfiguration<ContactAddress>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ContactAddress> builder)
    {
        builder.ToTable("contacts_addresses");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, v => ContactAddressId.From(v));
        builder.Property(a => a.ContactId).HasConversion(id => id.Value, v => ContactId.From(v));

        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Street1).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Street2).HasMaxLength(200);
        builder.Property(a => a.City).HasMaxLength(100).IsRequired();
        builder.Property(a => a.State).HasMaxLength(100);
        builder.Property(a => a.PostalCode).HasMaxLength(20);
        builder.Property(a => a.CountryCode).HasMaxLength(3).IsRequired();

        builder.HasIndex(a => a.ContactId);
    }
}
