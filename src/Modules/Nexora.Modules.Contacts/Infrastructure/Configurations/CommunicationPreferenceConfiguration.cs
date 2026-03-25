using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Infrastructure.Configurations;

/// <summary>EF Core configuration for the CommunicationPreference entity.</summary>
public sealed class CommunicationPreferenceConfiguration : IEntityTypeConfiguration<CommunicationPreference>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CommunicationPreference> builder)
    {
        builder.ToTable("contacts_communication_preferences");
        builder.HasKey(cp => cp.Id);
        builder.Property(cp => cp.Id).HasConversion(id => id.Value, v => CommunicationPreferenceId.From(v));
        builder.Property(cp => cp.ContactId).HasConversion(id => id.Value, v => ContactId.From(v));
        builder.Property(cp => cp.Channel).HasConversion<string>().HasMaxLength(50);
        builder.Property(cp => cp.OptInSource).HasMaxLength(100);

        builder.HasIndex(cp => new { cp.ContactId, cp.Channel }).IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}
