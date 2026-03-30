using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Domain.Repositories;
using Nexora.Modules.Audit.Domain.ValueObjects;

namespace Nexora.Modules.Audit.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IAuditEntryRepository"/>.</summary>
public sealed class AuditEntryRepository(AuditDbContext dbContext) : IAuditEntryRepository
{
    /// <inheritdoc />
    public async Task<AuditEntry?> GetByIdAsync(AuditEntryId id, string tenantId, CancellationToken ct)
    {
        return await dbContext.AuditEntries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId, ct);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<AuditEntry> Items, int TotalCount)> GetPagedAsync(
        string tenantId, int page, int pageSize,
        string? module, string? operation, Guid? userId,
        string? entityType, bool? isSuccess,
        DateTimeOffset? dateFrom, DateTimeOffset? dateTo,
        CancellationToken ct)
    {
        var query = dbContext.AuditEntries.AsNoTracking()
            .Where(e => e.TenantId == tenantId);

        if (module is not null)
            query = query.Where(e => e.Module == module);

        if (operation is not null)
            query = query.Where(e => e.Operation == operation);

        if (userId is not null)
            query = query.Where(e => e.UserId == userId);

        if (entityType is not null)
            query = query.Where(e => e.EntityType == entityType);

        if (isSuccess is not null)
            query = query.Where(e => e.IsSuccess == isSuccess);

        if (dateFrom is not null)
            query = query.Where(e => e.Timestamp >= dateFrom);

        if (dateTo is not null)
            query = query.Where(e => e.Timestamp <= dateTo);

        var totalCount = await query.CountAsync(ct);

        var items = await query.OrderByDescending(e => e.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
