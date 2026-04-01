using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Persistence;
using Nexora.Infrastructure.Persistence.Inbox;
using Nexora.Infrastructure.Persistence.Outbox;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Infrastructure;

/// <summary>EF Core database context for the Notifications module.</summary>
public sealed class NotificationsDbContext(
    DbContextOptions<NotificationsDbContext> options,
    ITenantContextAccessor tenantContextAccessor,
    DomainEventDispatcher? domainEventDispatcher = null)
    : BaseDbContext(options, tenantContextAccessor, domainEventDispatcher)
{
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<NotificationTemplateTranslation> NotificationTemplateTranslations => Set<NotificationTemplateTranslation>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();
    public DbSet<NotificationProvider> NotificationProviders => Set<NotificationProvider>();
    public DbSet<NotificationSchedule> NotificationSchedules => Set<NotificationSchedule>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        ApplySoftDeleteFilters(modelBuilder);
    }
}
