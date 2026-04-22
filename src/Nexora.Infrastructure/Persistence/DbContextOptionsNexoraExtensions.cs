using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    /// registration. Safe to call when <see cref="AuditChangeTrackerInterceptor"/> is not
    /// registered (e.g. in design-time / migration contexts) — the call no-ops in that case.
    /// </summary>
    public static DbContextOptionsBuilder AddNexoraAuditInterceptor(
        this DbContextOptionsBuilder options,
        IServiceProvider sp)
    {
        var interceptor = sp.GetService<AuditChangeTrackerInterceptor>();
        if (interceptor is not null)
            options.AddInterceptors(interceptor);
        return options;
    }
}
