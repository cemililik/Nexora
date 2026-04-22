using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexora.Infrastructure.Audit;

namespace Nexora.Infrastructure.Persistence;

/// <summary>
/// DbContext option helpers that apply Nexora-wide conventions (audit change capture,
/// future cross-cutting interceptors) to each module's context registration.
/// </summary>
public static class DbContextOptionsNexoraExtensions
{
    /// <summary>
    /// Attaches the audit change-tracker interceptor so entity diffs are captured on every
    /// <c>SaveChanges</c>. Call from each module's <c>AddDbContext((sp, options) =&gt; ...)</c>
    /// registration.
    /// In production the interceptor must be registered — a missing registration is a
    /// configuration error and throws. In all other environments a warning is logged and
    /// the call no-ops (design-time / migration scenarios where full DI is unavailable).
    /// </summary>
    public static DbContextOptionsBuilder AddNexoraAuditInterceptor(
        this DbContextOptionsBuilder options,
        IServiceProvider sp)
    {
        var interceptor = sp.GetService<AuditChangeTrackerInterceptor>();
        if (interceptor is not null)
        {
            options.AddInterceptors(interceptor);
            return options;
        }

        var env = sp.GetService<IHostEnvironment>();
        if (env?.IsProduction() == true)
            throw new InvalidOperationException(
                $"{nameof(AuditChangeTrackerInterceptor)} is not registered. " +
                "Call AddNexoraAuditInfrastructure() during service registration.");

        var logger = sp.GetService<ILoggerFactory>()
            ?.CreateLogger(nameof(DbContextOptionsNexoraExtensions));
        logger?.LogWarning(
            "{Interceptor} is not registered; entity change capture is disabled for this DbContext. " +
            "This is expected in design-time/migration scenarios.",
            nameof(AuditChangeTrackerInterceptor));

        return options;
    }
}
