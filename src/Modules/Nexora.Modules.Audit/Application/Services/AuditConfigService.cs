using Microsoft.EntityFrameworkCore;
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
    ITenantContextAccessor tenantContextAccessor) : IAuditConfigService
{
    private static readonly CacheOptions CacheTtl = new()
    {
        L1Ttl = TimeSpan.FromMinutes(2),
        L2Ttl = TimeSpan.FromMinutes(15)
    };

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(string module, string operation, CancellationToken ct, bool defaultEnabled = true)
    {
        var tenantId = tenantContextAccessor.Current.TenantId;
        var cacheKey = $"audit:config:{module}:{operation}:{(defaultEnabled ? "1" : "0")}";

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
        // 1. Check operation-level setting
        var operationSetting = await dbContext.AuditSettings.AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.TenantId == tenantId &&
                s.Module == module &&
                s.Operation == operation, ct);

        if (operationSetting is not null)
            return operationSetting.IsEnabled;

        // 2. Check module-level setting (operation = "*")
        var moduleSetting = await dbContext.AuditSettings.AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.TenantId == tenantId &&
                s.Module == module &&
                s.Operation == "*", ct);

        if (moduleSetting is not null)
            return moduleSetting.IsEnabled;

        // 3. Check global setting (module = "*", operation = "*")
        var globalSetting = await dbContext.AuditSettings.AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.TenantId == tenantId &&
                s.Module == "*" &&
                s.Operation == "*", ct);

        if (globalSetting is not null)
            return globalSetting.IsEnabled;

        // 4. Default: use the caller-specified default (commands default enabled, queries default disabled)
        return defaultEnabled;
    }
}
