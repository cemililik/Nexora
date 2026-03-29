namespace Nexora.Modules.Audit.Application.Services;

/// <summary>
/// Centralized audit config cache key builder. Ensures consistent key format
/// across AuditConfigService, UpdateAuditSettingHandler, and BulkUpdateAuditSettingsHandler.
/// </summary>
public static class AuditCacheKeys
{
    /// <summary>
    /// Builds the cache key for an audit config entry.
    /// Format: audit:{module}:{tenantId}:config:{operation}:{defaultFlag}
    /// </summary>
    public static string ConfigKey(string tenantId, string module, string operation, bool defaultEnabled) =>
        $"audit:{module}:{tenantId}:config:{operation}:{(defaultEnabled ? "1" : "0")}";

    /// <summary>
    /// Returns both defaultEnabled variant keys for cache invalidation.
    /// </summary>
    public static (string Enabled, string Disabled) InvalidationKeys(string tenantId, string module, string operation) =>
        (ConfigKey(tenantId, module, operation, true), ConfigKey(tenantId, module, operation, false));
}
