using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;

namespace Nexora.Modules.Notifications.Infrastructure.Configurations;

/// <summary>EF Core configuration for the NotificationTemplate entity.</summary>
public sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("notifications_templates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasConversion(id => id.Value, v => NotificationTemplateId.From(v));

        builder.Property(t => t.Code).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Module).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Channel).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(t => t.Subject).HasMaxLength(500).IsRequired();
        builder.Property(t => t.Body).IsRequired();
        builder.Property(t => t.Format).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasIndex(t => new { t.TenantId, t.Code, t.Channel }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(t => new { t.TenantId, t.Module });

        builder.HasMany(t => t.Translations).WithOne().HasForeignKey(tr => tr.TemplateId);
        builder.Navigation(t => t.Translations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
