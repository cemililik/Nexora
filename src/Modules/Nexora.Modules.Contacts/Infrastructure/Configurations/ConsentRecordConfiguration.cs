using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Infrastructure.Configurations;

public sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("contacts_consent_records");
        builder.HasKey(cr => cr.Id);
        builder.Property(cr => cr.Id).HasConversion(id => id.Value, v => ConsentRecordId.From(v));
        builder.Property(cr => cr.ContactId).HasConversion(id => id.Value, v => ContactId.From(v));
        builder.Property(cr => cr.ConsentType).HasConversion<string>().HasMaxLength(50);
        builder.Property(cr => cr.Source).HasMaxLength(100);
        builder.Property(cr => cr.IpAddress).HasMaxLength(45);

        builder.HasIndex(cr => cr.ContactId);
        builder.HasIndex(cr => new { cr.ContactId, cr.ConsentType });
    }
}
