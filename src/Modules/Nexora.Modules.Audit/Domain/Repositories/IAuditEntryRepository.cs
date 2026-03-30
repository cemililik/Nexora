using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Domain.ValueObjects;

namespace Nexora.Modules.Audit.Domain.Repositories;

/// <summary>Repository interface for querying audit log entries.</summary>
public interface IAuditEntryRepository
{
    /// <summary>Retrieves a single audit entry by its identifier within a tenant.</summary>
    Task<AuditEntry?> GetByIdAsync(AuditEntryId id, string tenantId, CancellationToken ct);

    /// <summary>Returns a paginated, filtered list of audit entries for a tenant.</summary>
    Task<(IReadOnlyList<AuditEntry> Items, int TotalCount)> GetPagedAsync(
        string tenantId, int page, int pageSize,
        string? module, string? operation, Guid? userId,
        string? entityType, bool? isSuccess,
        DateTimeOffset? dateFrom, DateTimeOffset? dateTo,
        CancellationToken ct);
}
