using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Infrastructure.Audit;
using Nexora.Infrastructure.Authorization;
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
using Nexora.SharedKernel.Authorization;
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
        // Permission registry (ADR-004 / permissions.md §3) — populated by each module's
        // OnStartupAsync hook, consumed by IdentityModuleMigration.SeedAsync.
        services.AddSingleton<IPermissionRegistry, InMemoryPermissionRegistry>();

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
        services.AddSingleton<IValidateOptions<OutboxOptions>, OutboxOptionsValidator>();
        services.AddDbContext<OutboxDbContext>((_, options) =>
        {
            var connStr = configuration.GetConnectionString("Default");
            options.UseNpgsql(connStr);
        });
        // IOutbox registered per-module as OutboxService<TContext> for transactional atomicity
        services.AddHostedService<OutboxProcessor>();
        // T-023: the readiness probe now covers Postgres + the Dapr sidecar in
        // addition to the outbox. Keycloak and MinIO stay transitive via Dapr
        // — see T-023's task file for rationale.
        services.AddHttpClient(
            Messaging.DaprSidecarHealthCheck.HttpClientName,
            c => c.Timeout = Messaging.DaprSidecarHealthCheck.ProbeTimeout);
        services.AddHealthChecks()
            .AddCheck<OutboxHealthCheck>("outbox", tags: ["ready"])
            .AddCheck<Persistence.PostgresHealthCheck>("postgres", tags: ["ready"])
            .AddCheck<Messaging.DaprSidecarHealthCheck>("dapr-sidecar", tags: ["ready"]);

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

        // ADR-0025: three-tier configuration resolver (platform cap → tenant default → org
        // override). NullComplianceCapProvider is the default; SaaS deployments replace it
        // with NmpComplianceCapProvider in NMP.2.
        services.AddScoped<IConfigurationResolver, DatabaseConfigurationResolver>();
        // Scoped (not singleton) so future tenant-aware implementations (NmpComplianceCapProvider
        // in NMP.2) can take scoped dependencies like TenantConfigDbContext. NullComplianceCapProvider
        // is stateless and works fine as scoped; no behavioural change here.
        services.AddScoped<IComplianceCapProvider, NullComplianceCapProvider>();

        // T-005 Demo Data Framework — orchestrator + tenant-scoped idempotency marker store.
        services.AddDbContext<Modules.DemoSeedMarkerDbContext>((_, options) =>
        {
            var connStr = configuration.GetConnectionString("Default");
            options.UseNpgsql(connStr);
        });
        // DemoDataSeeder owns its scopes (uses IServiceScopeFactory + creates
        // an async scope per module + per filter/markers query), so the
        // orchestrator instance itself is stateless and safe to share. Singleton
        // matches its lifecycle requirements — its three ctor deps
        // (IServiceScopeFactory, IEnumerable<IModule>, ILogger<T>) are all
        // root-resolvable. Scoped registration would force a fresh seeder per
        // tenant request without any benefit.
        services.AddSingleton<IDemoDataSeeder, Modules.DemoDataSeeder>();
        // T-009: orchestrator-level counterpart to IDemoDataSeeder. Scoped
        // because its IEventBus dep resolves through the Dapr client chain
        // that may not be singleton-safe under every hosting model; the CLI
        // verb / admin UI both create a scope before resolving, so per-call
        // scoping is the right default.
        services.AddScoped<IDemoDataCleaner, Modules.DemoDataCleaner>();
        // T-029: scenario catalogue. Singleton — scenarios are immutable
        // records frozen at construction; per-tenant filtering takes a
        // fresh DI scope inside the registry, so the singleton itself
        // holds no per-tenant state.
        services.AddSingleton<IDemoScenarioRegistry, Modules.InMemoryDemoScenarioRegistry>();

        // T-011: platform-level migration orchestrator + the public-schema
        // failure-log DbContext it writes through. Singleton matches the
        // dep shape (IServiceScopeFactory + IEnumerable<IModule> + ILogger
        // + a captured connection-string string) and the post-deploy
        // platform:migrate-tenants Hangfire job calls it from a single
        // outer scope per tenant.
        services.AddDbContext<Migrations.MigrationFailureLogDbContext>((_, options) =>
        {
            var connStr = configuration.GetConnectionString("Default");
            options.UseNpgsql(connStr);
        });
        services.AddSingleton<SharedKernel.Abstractions.Migrations.IMigrationRunner>(sp =>
            new Migrations.MigrationRunner(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<IEnumerable<SharedKernel.Abstractions.Modules.IModule>>(),
                sp.GetRequiredService<ILogger<Migrations.MigrationRunner>>(),
                configuration.GetConnectionString("Default")
                    ?? throw new InvalidOperationException("Default connection string is required for IMigrationRunner.")));

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

        // TODO(NMP): Replace NullLicenseVerifier with a real implementation when the NMP track is built.
        // The "Nexora:DeploymentMode" setting in appsettings.json ("OnPrem" | "SaaS") should be used
        // here to conditionally register the on-prem verifier vs the SaaS/NMP verifier:
        //   var mode = configuration["Nexora:DeploymentMode"];
        //   if (mode == "SaaS") services.AddSingleton<ILicenseVerifier, NmpLicenseVerifier>();
        //   else                services.AddSingleton<ILicenseVerifier, NullLicenseVerifier>();
        services.AddSingleton<ILicenseVerifier, NullLicenseVerifier>();

        // Audit context (requires IHttpContextAccessor)
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditContext, HttpAuditContext>();

        // Entity change capture: scoped buffer written by the EF interceptor and read by AuditLogBehavior
        services.AddScoped<IAuditStateCapture, AuditStateCapture>();
        services.AddScoped<AuditChangeTrackerInterceptor>();

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
