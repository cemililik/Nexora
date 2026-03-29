using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Audit.Infrastructure;
using Nexora.SharedKernel.Abstractions.Audit;
using Nexora.SharedKernel.Abstractions.Caching;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Audit.Application.Services;

/// <summary>
/// Determines whether audit logging is enabled for a given module/operation.
/// Resolution order: operation-level setting > module-level setting > global setting > default (true).
/// Results are cached with a 15-minute TTL.
/// </summary>
public sealed class AuditConfigService(
    AuditDbContext dbContext,
    ICacheService cacheService,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<AuditConfigService> logger) : IAuditConfigService
{
    private static readonly CacheOptions CacheTtl = new()
    {
        L1Ttl = TimeSpan.FromMinutes(2),
        L2Ttl = TimeSpan.FromMinutes(15)
    };

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(string module, string operation, CancellationToken ct, bool defaultEnabled = true)
    {
        module = module.Trim().ToLowerInvariant();
        operation = operation.Trim().ToLowerInvariant();

        var tenantId = tenantContextAccessor.Current.TenantId;
        var cacheKey = AuditCacheKeys.ConfigKey(tenantId, module, operation, defaultEnabled);

        // Cache a string value ("1"/"0") to avoid value-type serialization issues with bool
        var cached = await cacheService.GetOrSetAsync(
            cacheKey,
            async token => await ResolveFromDatabase(tenantId, module, operation, defaultEnabled, token) ? "1" : "0",
            CacheTtl,
            ct);

        return cached == "1";
    }

    private async Task<bool> ResolveFromDatabase(string tenantId, string module, string operation, bool defaultEnabled, CancellationToken ct)
    {
        logger.LogDebug("Audit config cache miss for {TenantId}:{Module}.{Operation}, resolving from database", tenantId, module, operation);

        // Single query fetching the three valid setting levels:
        //   1. Exact module + exact operation
        //   2. Exact module + wildcard operation (module-level)
        //   3. Wildcard module + wildcard operation (global)
        // Note: Module="*" + Operation=exact is invalid and excluded.
        var settings = await dbContext.AuditSettings.AsNoTracking()
            .Where(s =>
                s.TenantId == tenantId &&
                (s.Module == module || s.Module == "*") &&
                (s.Operation == operation || s.Operation == "*") &&
                !(s.Module == "*" && s.Operation != "*"))
            .ToListAsync(ct);

        var best = settings
            .OrderBy(s => s.Module == "*" ? 1 : 0)
            .ThenBy(s => s.Operation == "*" ? 1 : 0)
            .FirstOrDefault();

        return best?.IsEnabled ?? defaultEnabled;
    }
}
