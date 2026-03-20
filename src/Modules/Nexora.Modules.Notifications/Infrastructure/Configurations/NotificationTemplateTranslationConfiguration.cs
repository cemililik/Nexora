using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;

namespace Nexora.Modules.Notifications.Infrastructure.Configurations;

/// <summary>EF Core configuration for the NotificationTemplateTranslation entity.</summary>
public sealed class NotificationTemplateTranslationConfiguration : IEntityTypeConfiguration<NotificationTemplateTranslation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NotificationTemplateTranslation> builder)
    {
        builder.ToTable("notifications_template_translations");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasConversion(id => id.Value, v => NotificationTemplateTranslationId.From(v));
        builder.Property(t => t.TemplateId).HasConversion(id => id.Value, v => NotificationTemplateId.From(v));

        builder.Property(t => t.LanguageCode).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Subject).HasMaxLength(500).IsRequired();
        builder.Property(t => t.Body).IsRequired();

        builder.HasIndex(t => new { t.TemplateId, t.LanguageCode }).IsUnique();
    }
}
