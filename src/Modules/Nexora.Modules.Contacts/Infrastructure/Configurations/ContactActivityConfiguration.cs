using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Infrastructure.Configurations;

public sealed class ContactActivityConfiguration : IEntityTypeConfiguration<ContactActivity>
{
    public void Configure(EntityTypeBuilder<ContactActivity> builder)
    {
        builder.ToTable("contacts_activities");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, v => ContactActivityId.From(v));
        builder.Property(a => a.ContactId).HasConversion(id => id.Value, v => ContactId.From(v));

        builder.Property(a => a.ModuleSource).HasMaxLength(50).IsRequired();
        builder.Property(a => a.ActivityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Summary).HasMaxLength(500).IsRequired();
        builder.Property(a => a.Details).HasColumnType("jsonb");

        builder.HasIndex(a => a.ContactId);
        builder.HasIndex(a => a.OccurredAt);
    }
}
