using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;

namespace Nexora.Modules.Documents.Application.Services;

/// <summary>
/// Service for checking and filtering document access based on user permissions.
/// </summary>
public interface IDocumentAccessChecker
{
    /// <summary>Checks if a user has access to a specific document.</summary>
    /// <param name="documentId">Document identifier.</param>
    /// <param name="userId">User identifier.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="roleIds">Role identifiers the user belongs to (for role-based access).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the user has access; otherwise false.</returns>
    Task<bool> HasAccessAsync(
        DocumentId documentId, Guid userId, Guid tenantId,
        IReadOnlyList<Guid>? roleIds = null, CancellationToken ct = default);

    /// <summary>Applies access filtering as a DB-level query expression. Returns only documents
    /// the user owns or has explicit access to (user-based or role-based).</summary>
    /// <param name="query">Source queryable of documents.</param>
    /// <param name="userId">User identifier.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="roleIds">Role identifiers the user belongs to (for role-based access).</param>
    /// <returns>Filtered queryable — no materialization occurs.</returns>
    IQueryable<Document> ApplyAccessFilter(
        IQueryable<Document> query, Guid userId, Guid tenantId,
        IReadOnlyList<Guid>? roleIds = null);
}
