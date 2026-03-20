using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;

namespace Nexora.Modules.Notifications.Infrastructure.Configurations;

/// <summary>EF Core configuration for the Notification entity.</summary>
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications_notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasConversion(id => id.Value, v => NotificationId.From(v));
        builder.Property(n => n.TemplateId).HasConversion(
            id => id.HasValue ? id.Value.Value : (Guid?)null,
            v => v.HasValue ? NotificationTemplateId.From(v.Value) : null);

        builder.Property(n => n.Channel).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(n => n.Subject).HasMaxLength(500).IsRequired();
        builder.Property(n => n.BodyRendered).IsRequired();
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(n => n.TriggeredBy).HasMaxLength(100).IsRequired();

        builder.HasIndex(n => new { n.TenantId, n.Status });
        builder.HasIndex(n => n.TriggeredByUserId);
        builder.HasIndex(n => n.QueuedAt);

        builder.HasMany(n => n.Recipients).WithOne().HasForeignKey(r => r.NotificationId);
        builder.Navigation(n => n.Recipients).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
