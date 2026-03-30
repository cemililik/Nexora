using Nexora.Modules.Audit.Domain.Entities;

namespace Nexora.Modules.Audit.Domain.Repositories;

/// <summary>Repository interface for managing audit setting entities.</summary>
public interface IAuditSettingRepository
{
    /// <summary>Returns all audit settings for a given tenant, ordered by module then operation.</summary>
    Task<IReadOnlyList<AuditSetting>> GetAllByTenantAsync(string tenantId, CancellationToken ct);

    /// <summary>Finds a single audit setting by exact tenant, module, and operation match.</summary>
    Task<AuditSetting?> FindByKeyAsync(string tenantId, string module, string operation, CancellationToken ct);

    /// <summary>
    /// Finds audit settings matching the config resolution hierarchy:
    /// exact module+operation, module+wildcard, or global wildcard.
    /// Excludes invalid wildcard-module with exact-operation combinations.
    /// </summary>
    Task<IReadOnlyList<AuditSetting>> FindConfigSettingsAsync(string tenantId, string module, string operation, CancellationToken ct);

    /// <summary>Adds a new audit setting to the context.</summary>
    void Add(AuditSetting setting);

    /// <summary>Persists all pending changes to the database.</summary>
    Task SaveChangesAsync(CancellationToken ct);
}
