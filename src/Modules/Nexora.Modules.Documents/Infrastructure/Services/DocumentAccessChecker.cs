using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Documents.Application.Services;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;

namespace Nexora.Modules.Documents.Infrastructure.Services;

/// <summary>
/// Checks document access by querying the DocumentAccess table.
/// A user has access if they uploaded the document OR have an explicit access record (by userId or roleId).
/// </summary>
public sealed class DocumentAccessChecker(DocumentsDbContext dbContext) : IDocumentAccessChecker
{
    /// <inheritdoc />
    public async Task<bool> HasAccessAsync(
        DocumentId documentId, Guid userId, Guid tenantId,
        IReadOnlyList<Guid>? roleIds = null, CancellationToken ct = default)
    {
        // Single query: owner OR user-access OR role-access
        return await dbContext.Documents
            .Where(d => d.Id == documentId && d.TenantId == tenantId)
            .Where(d => d.UploadedByUserId == userId
                || d.AccessList.Any(a => a.UserId == userId)
                || (roleIds != null && roleIds.Count > 0 && d.AccessList.Any(a => a.RoleId != null && roleIds.Contains(a.RoleId.Value))))
            .AnyAsync(ct);
    }

    /// <inheritdoc />
    public IQueryable<Document> ApplyAccessFilter(
        IQueryable<Document> query, Guid userId, Guid tenantId,
        IReadOnlyList<Guid>? roleIds = null)
    {
        return query.Where(d =>
            d.UploadedByUserId == userId
            || d.AccessList.Any(a => a.UserId == userId)
            || (roleIds != null && roleIds.Count > 0 && d.AccessList.Any(a => a.RoleId != null && roleIds.Contains(a.RoleId.Value))));
    }
}
