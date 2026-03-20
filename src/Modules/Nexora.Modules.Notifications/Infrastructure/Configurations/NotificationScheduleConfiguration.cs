using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;

namespace Nexora.Modules.Notifications.Infrastructure.Configurations;

/// <summary>EF Core configuration for the NotificationSchedule entity.</summary>
public sealed class NotificationScheduleConfiguration : IEntityTypeConfiguration<NotificationSchedule>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NotificationSchedule> builder)
    {
        builder.ToTable("notifications_schedules");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasConversion(id => id.Value, v => NotificationScheduleId.From(v));
        builder.Property(s => s.NotificationId).HasConversion(id => id.Value, v => NotificationId.From(v));

        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasIndex(s => new { s.Status, s.ScheduledAt });
    }
}
