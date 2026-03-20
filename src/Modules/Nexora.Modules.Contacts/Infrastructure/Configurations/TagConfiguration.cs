using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Infrastructure.Configurations;

/// <summary>EF Core configuration for the Tag entity.</summary>
public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("contacts_tags");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasConversion(id => id.Value, v => TagId.From(v));

        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Color).HasMaxLength(20);
        builder.Property(t => t.Category).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(t => new { t.TenantId, t.Name }).IsUnique();
    }
}
