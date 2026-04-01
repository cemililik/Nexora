using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Infrastructure.Audit;
using Nexora.Infrastructure.Behaviors;
using Nexora.Infrastructure.Persistence;
using Nexora.Infrastructure.Caching;
using Nexora.Infrastructure.Configuration;
using Nexora.Infrastructure.Jobs;
using Nexora.Infrastructure.Localization;
using Nexora.Infrastructure.Messaging;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Infrastructure.Secrets;
using Nexora.Infrastructure.Persistence.Inbox;
using Nexora.Infrastructure.Persistence.Outbox;
using Nexora.Infrastructure.Storage;
using Nexora.SharedKernel.Abstractions.Audit;
using Nexora.SharedKernel.Abstractions.Caching;
using Nexora.SharedKernel.Abstractions.Configuration;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.Licensing;
using Nexora.SharedKernel.Abstractions.Localization;
using Nexora.SharedKernel.Abstractions.Secrets;
using Nexora.SharedKernel.Abstractions.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.Infrastructure.Licensing;

namespace Nexora.Infrastructure;

/// <summary>
/// Registers all cross-cutting infrastructure services (tenancy, caching, messaging, jobs, etc.).
/// </summary>
public static class InfrastructureServiceRegistration
{
    /// <summary>Adds Nexora infrastructure services to the DI container.</summary>
    public static IServiceCollection AddNexoraInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Multi-tenancy
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
        services.AddSingleton<ITenantSchemaManager>(sp =>
        {
            var connStr = configuration.GetConnectionString("Default")!;
            var migrations = sp.GetServices<IModuleMigration>();
            var logger = sp.GetRequiredService<ILogger<TenantSchemaManager>>();
            return new TenantSchemaManager(connStr, migrations, logger);
        });
        services.AddScoped<IActiveTenantProvider, PlatformTenantProvider>();

        // MediatR — register core services so IPublisher is available for DomainEventDispatcher.
        // Each module adds its own handlers via AddMediatR(cfg => cfg.RegisterServicesFromAssembly(...)).
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(InfrastructureServiceRegistration).Assembly));

        // Domain event dispatching
        services.AddOptions<DomainEventChannelOptions>()
            .BindConfiguration("DomainEvents")
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DomainEventChannelOptions>, DomainEventChannelOptionsValidator>();
        services.AddSingleton<DomainEventChannel>();
        services.AddScoped<DomainEventDispatcher>();
        services.AddHostedService<DomainEventBackgroundProcessor>();

        // Caching
        services.AddMemoryCache();
        services.AddScoped<ICacheService, DaprCacheService>();
        services.AddSingleton<CacheInvalidationHandler>();

        // Messaging
        services.AddScoped<IEventBus, DaprEventBus>();

        // Outbox (reliable event publishing)
        services.Configure<OutboxOptions>(
            configuration.GetSection(OutboxOptions.SectionName));
        services.AddDbContext<OutboxDbContext>((_, options) =>
        {
            var connStr = configuration.GetConnectionString("Default");
            options.UseNpgsql(connStr);
        });
        // IOutbox registered per-module as OutboxService<TContext> for transactional atomicity
        services.AddHostedService<OutboxProcessor>();
        services.AddHealthChecks()
            .AddCheck<OutboxHealthCheck>("outbox", tags: ["ready"]);

        // Secrets
        services.AddScoped<ISecretProvider, DaprSecretProvider>();

        // File Storage (MinIO)
        services.Configure<MinioStorageOptions>(
            configuration.GetSection(MinioStorageOptions.SectionName));
        services.Configure<StorageOptions>(
            configuration.GetSection(StorageOptions.SectionName));
        services.AddScoped<IFileStorageService, MinioFileStorageService>();

        // Tenant configuration
        services.AddDbContext<TenantConfigDbContext>((sp, options) =>
        {
            var connStr = configuration.GetConnectionString("Default");
            options.UseNpgsql(connStr);
        });
        services.AddScoped<ITenantConfiguration, DatabaseTenantConfiguration>();

        // Localization
        services.AddDbContext<LocalizationDbContext>((_, options) =>
        {
            var connStr = configuration.GetConnectionString("Default");
            options.UseNpgsql(connStr);
        });
        services.AddScoped<ILocalizationService, DatabaseLocalizationService>();

        // Job scheduler
        services.AddSingleton<IJobScheduler, HangfireJobScheduler>();

        // Hangfire
        var connectionString = configuration.GetConnectionString("Hangfire")
            ?? configuration.GetConnectionString("Default");

        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddHangfire((sp, config) =>
            {
                config.UsePostgreSqlStorage(opts =>
                    opts.UseNpgsqlConnection(connectionString));

                // Tenant-aware job filter
                config.UseFilter(new TenantJobFilter(sp));
            });

            services.AddHangfireServer(options =>
            {
                options.Queues = JobQueues.All;
                options.WorkerCount = Environment.ProcessorCount * 2;
            });
        }

        // Infrastructure cleanup jobs
        services.AddScoped<OutboxCleanupJob>();
        services.AddScoped<InboxCleanupJob>();

        // License verification — NullLicenseVerifier (always allowed) until NMP track is implemented
        services.AddSingleton<ILicenseVerifier, NullLicenseVerifier>();

        // Audit context (requires IHttpContextAccessor)
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditContext, HttpAuditContext>();

        // MediatR behaviors
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditLogBehavior<,>));

        // FluentValidation — deferred registration.
        // Validators are registered after module assemblies are loaded by AddNexoraModules().
        // Each module can also register validators in its own ConfigureServices().

        return services;
    }

    /// <summary>
    /// Registers infrastructure-level recurring Hangfire jobs (outbox/inbox cleanup).
    /// Call this after the application is built and Hangfire is initialized.
    /// </summary>
    public static void ConfigureInfrastructureJobs()
    {
        // Outbox cleanup — runs daily at 02:00 UTC, deletes processed messages older than configured days
        RecurringJob.AddOrUpdate<OutboxCleanupJob>(
            "infrastructure:outbox-cleanup",
            JobQueues.Maintenance,
            job => job.RunAsync(CancellationToken.None),
            "0 2 * * *",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        // Inbox cleanup — runs daily at 02:30 UTC, deletes inbox messages older than 30 days per tenant
        RecurringJob.AddOrUpdate<InboxCleanupJob>(
            "infrastructure:inbox-cleanup",
            JobQueues.Maintenance,
            job => job.RunAsync(
                new InboxCleanupJobParams { TenantId = "system", RetentionDays = 30 },
                CancellationToken.None),
            "30 2 * * *",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}
