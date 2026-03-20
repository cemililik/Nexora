using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;

namespace Nexora.Modules.Notifications.Infrastructure.Configurations;

/// <summary>EF Core configuration for the NotificationRecipient entity.</summary>
public sealed class NotificationRecipientConfiguration : IEntityTypeConfiguration<NotificationRecipient>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NotificationRecipient> builder)
    {
        builder.ToTable("notifications_recipients");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, v => NotificationRecipientId.From(v));
        builder.Property(r => r.NotificationId).HasConversion(id => id.Value, v => NotificationId.From(v));

        builder.Property(r => r.RecipientAddress).HasMaxLength(256).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(r => r.FailureReason).HasMaxLength(500);
        builder.Property(r => r.ProviderMessageId).HasMaxLength(200);

        builder.HasIndex(r => new { r.NotificationId, r.Status });
        builder.HasIndex(r => r.ContactId);
    }
}
