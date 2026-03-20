using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Infrastructure.Configurations;

/// <summary>EF Core configuration for the Contact entity.</summary>
public sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("contacts_contacts");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, v => ContactId.From(v));
        builder.Property(c => c.MergedIntoId).HasConversion(
            id => id.HasValue ? id.Value.Value : (Guid?)null,
            v => v.HasValue ? ContactId.From(v.Value) : null);

        builder.Property(c => c.Title).HasMaxLength(20);
        builder.Property(c => c.FirstName).HasMaxLength(100);
        builder.Property(c => c.LastName).HasMaxLength(100);
        builder.Property(c => c.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.CompanyName).HasMaxLength(200);
        builder.Property(c => c.Email).HasMaxLength(256);
        builder.Property(c => c.Phone).HasMaxLength(30);
        builder.Property(c => c.Mobile).HasMaxLength(30);
        builder.Property(c => c.Website).HasMaxLength(500);
        builder.Property(c => c.TaxId).HasMaxLength(50);
        builder.Property(c => c.Language).HasMaxLength(10);
        builder.Property(c => c.Currency).HasMaxLength(3);
        builder.Property(c => c.Source).HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.Metadata).HasColumnType("jsonb");
        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(c => new { c.TenantId, c.Email });
        builder.HasIndex(c => new { c.TenantId, c.Phone });
        builder.HasIndex(c => new { c.TenantId, c.Status });
        builder.HasIndex(c => new { c.TenantId, c.DisplayName });

        builder.HasMany(c => c.Addresses).WithOne().HasForeignKey(a => a.ContactId);
        builder.Navigation(c => c.Addresses).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(c => c.Tags).WithOne().HasForeignKey(t => t.ContactId);
        builder.Navigation(c => c.Tags).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
