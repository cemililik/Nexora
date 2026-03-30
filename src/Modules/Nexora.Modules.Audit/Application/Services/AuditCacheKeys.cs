namespace Nexora.Modules.Audit.Application.Services;

/// <summary>
/// Centralized audit config cache key builder. Ensures consistent key format
/// across AuditConfigService, UpdateAuditSettingHandler, and BulkUpdateAuditSettingsHandler.
/// Keys do NOT include tenant ID — DaprCacheService auto-prefixes it via ITenantContextAccessor.
/// </summary>
public static class AuditCacheKeys
{
    /// <summary>
    /// Builds the cache key for an audit config entry.
    /// Format: audit:{module}:config:{operation}:{defaultFlag}
    /// Tenant ID is automatically prepended by DaprCacheService.
    /// </summary>
    public static string ConfigKey(string module, string operation, bool defaultEnabled) =>
        $"audit:{module}:config:{operation}:{(defaultEnabled ? "1" : "0")}";

    /// <summary>
    /// Returns both defaultEnabled variant keys for cache invalidation.
    /// </summary>
    public static (string Enabled, string Disabled) InvalidationKeys(string module, string operation) =>
        (ConfigKey(module, operation, true), ConfigKey(module, operation, false));
}
