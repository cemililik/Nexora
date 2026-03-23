using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Modules.Notifications.Api;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;
using Nexora.Modules.Notifications.Infrastructure.Jobs;
using Nexora.Modules.Notifications.Infrastructure.Services;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
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
        });

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(NotificationsModule).Assembly));

        // Register module migration for tenant provisioning
        services.AddSingleton<IModuleMigration, NotificationsModuleMigration>();

        // Register cross-module notification service
        services.AddScoped<INotificationService, NotificationService>();
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
            JobQueues.Maintenance);

        scheduler.AddOrUpdate<NotificationCleanupJob>(
            "notifications:cleanup-old-notifications",
            "0 3 * * 0", // Every Sunday at 03:00 UTC
            JobQueues.Maintenance);

        scheduler.AddOrUpdate<ScheduledNotificationDispatcherJob>(
            "notifications:dispatch-scheduled",
            "*/5 * * * *", // Every 5 minutes
            JobQueues.Default);
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct)
    {
        return Task.FromResult(HealthCheckResult.Healthy());
    }

    /// <inheritdoc />
    public Task OnStartupAsync(CancellationToken ct)
    {
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
