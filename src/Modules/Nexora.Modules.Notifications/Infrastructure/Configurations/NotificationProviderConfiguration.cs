using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;

namespace Nexora.Modules.Notifications.Infrastructure.Configurations;

/// <summary>EF Core configuration for the NotificationProvider entity.</summary>
public sealed class NotificationProviderConfiguration : IEntityTypeConfiguration<NotificationProvider>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NotificationProvider> builder)
    {
        builder.ToTable("notifications_providers");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion(id => id.Value, v => NotificationProviderId.From(v));

        builder.Property(p => p.Channel).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.ProviderName).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(p => p.Config).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(p => new { p.TenantId, p.Channel, p.ProviderName }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(p => new { p.TenantId, p.Channel, p.IsDefault });
    }
}
