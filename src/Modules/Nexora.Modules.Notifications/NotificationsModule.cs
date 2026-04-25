using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Infrastructure.Persistence;
using Nexora.Infrastructure.Persistence.Inbox;
using Nexora.Infrastructure.Persistence.Outbox;
using Nexora.Modules.Notifications.Api;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;
using Nexora.Modules.Notifications.Infrastructure.Jobs;
using Nexora.Modules.Notifications.Infrastructure.Services;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Authorization;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications;

/// <summary>Module entry point for the Notifications module, handling registration, endpoints, and jobs.</summary>
public sealed class NotificationsModule : IModule
{
    public string Name => "notifications";
    public string DisplayName => "lockey_notifications_module_display_name";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => ["identity", "contacts"];

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NotificationsDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("Default");
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications");
            });
            options.AddNexoraAuditInterceptor(sp);
        });

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(NotificationsModule).Assembly));

        // Register module migration for tenant provisioning
        services.AddSingleton<IModuleMigration, NotificationsModuleMigration>();

        // Register cross-module notification service
        services.AddScoped<INotificationService, NotificationService>();

        // Register inbox guard for idempotent integration event consumption
        services.AddScoped<IInboxGuard, InboxGuard<NotificationsDbContext>>();
        services.AddScoped<IOutbox, OutboxService<NotificationsDbContext>>();

        // T-027: GDPR escape hatch — discovers renamed _del_ tables for the
        // notifications module so Article-17 redaction reaches them too.
        services.AddScoped<
            Nexora.SharedKernel.Abstractions.Gdpr.IGdprRenamedTableScanner<NotificationsDbContext>,
            Nexora.Infrastructure.Gdpr.PostgresGdprRenamedTableScanner<NotificationsDbContext>>();
    }

    /// <inheritdoc />
    public void ConfigureEventHandlers(IServiceCollection services)
    {
        // Domain event handlers (NotificationSent/Delivered/Bounced → integration event bridge)
        // are auto-registered via MediatR assembly scanning.

        // Integration event handlers for cross-module events:
        services.AddScoped<IIntegrationEventHandler<UserCreatedIntegrationEvent>,
            UserCreatedIntegrationEventHandler>();
        services.AddScoped<IIntegrationEventHandler<ConsentChangedIntegrationEvent>,
            ConsentChangedIntegrationEventHandler>();
        services.AddScoped<IIntegrationEventHandler<ContactGdprDeletedIntegrationEvent>,
            ContactGdprDeletedIntegrationEventHandler>();
        services.AddScoped<IIntegrationEventHandler<ContactExportCompletedIntegrationEvent>,
            ContactExportCompletedNotificationHandler>();

        // Notification delivery via Kafka (replaces direct Hangfire job enqueuing):
        services.AddScoped<IIntegrationEventHandler<NotificationDeliveryRequestedIntegrationEvent>,
            NotificationDeliveryRequestedEventHandler>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapTemplateEndpoints();
        endpoints.MapProviderEndpoints();
        endpoints.MapNotificationEndpoints();
        endpoints.MapWebhookEndpoints();
        endpoints.MapBulkEndpoints();
        endpoints.MapScheduleEndpoints();
    }

    /// <inheritdoc />
    public void ConfigureJobs(IJobScheduler scheduler)
    {
        scheduler.AddOrUpdate<DailyProviderResetJob>(
            "notifications:daily-provider-reset",
            "0 0 * * *", // Every day at midnight UTC
            job => job.RunAsync(new DailyProviderResetJobParams { TenantId = "system" }, CancellationToken.None),
            JobQueues.Maintenance);

        scheduler.AddOrUpdate<NotificationCleanupJob>(
            "notifications:cleanup-old-notifications",
            "0 3 * * 0", // Every Sunday at 03:00 UTC
            job => job.RunAsync(new NotificationCleanupJobParams { TenantId = "system" }, CancellationToken.None),
            JobQueues.Maintenance);

        scheduler.AddOrUpdate<ScheduledNotificationDispatcherJob>(
            "notifications:dispatch-scheduled",
            "*/5 * * * *", // Every 5 minutes
            job => job.RunAsync(new ScheduledNotificationDispatcherJobParams { TenantId = "system" }, CancellationToken.None),
            JobQueues.Default);
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct)
    {
        return Task.FromResult(HealthCheckResult.Healthy());
    }

    /// <inheritdoc />
    public Task OnStartupAsync(IPermissionRegistry registry, CancellationToken ct)
    {
        registry.Register("notifications", "notification", "read", "lockey_notifications_permission_notification_read");
        registry.Register("notifications", "notification", "send", "lockey_notifications_permission_notification_send");
        registry.Register("notifications", "template", "read",   "lockey_notifications_permission_template_read");
        registry.Register("notifications", "template", "manage", "lockey_notifications_permission_template_manage");
        registry.Register("notifications", "provider", "read",   "lockey_notifications_permission_provider_read");
        registry.Register("notifications", "provider", "manage", "lockey_notifications_permission_provider_manage");
        registry.Register("notifications", "schedule", "read",   "lockey_notifications_permission_schedule_read");
        registry.Register("notifications", "schedule", "manage", "lockey_notifications_permission_schedule_manage");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnInstallAsync(TenantInstallContext context, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnUninstallAsync(TenantInstallContext context, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
