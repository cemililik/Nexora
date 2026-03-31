using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nexora.Infrastructure.Persistence.Inbox;

/// <summary>EF Core configuration for the InboxMessage entity.</summary>
public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");
        builder.HasKey(m => m.EventId);
        builder.Property(m => m.EventId).ValueGeneratedNever();
        builder.Property(m => m.EventType).HasMaxLength(500).IsRequired();
        builder.Property(m => m.ProcessedAt).IsRequired();

        builder.HasIndex(m => m.ProcessedAt);
    }
}
