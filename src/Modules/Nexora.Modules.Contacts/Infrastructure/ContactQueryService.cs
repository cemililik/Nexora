using Microsoft.EntityFrameworkCore;
using Nexora.SharedKernel.Abstractions.Caching;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Infrastructure;

/// <summary>
/// Implementation of IContactQueryService for cross-module contact data access.
/// </summary>
public sealed class ContactQueryService(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ICacheService cache) : IContactQueryService
{
    public async Task<ContactSummary?> GetByIdAsync(Guid contactId, CancellationToken ct = default)
    {
        var tenantId = tenantContextAccessor.Current.TenantId;
        var cacheKey = $"contacts:{tenantId}:contact:{contactId}";

        return await cache.GetOrSetAsync(
            cacheKey,
            async token =>
            {
                var tenantGuid = Guid.Parse(tenantId);
                var id = Domain.ValueObjects.ContactId.From(contactId);

                return await dbContext.Contacts
                    .Where(c => c.Id == id && c.TenantId == tenantGuid)
                    .AsNoTracking()
                    .Select(c => new ContactSummary(
                        c.Id.Value, c.DisplayName, c.Email, c.Phone,
                        c.Type.ToString(), c.Status.ToString()))
                    .FirstOrDefaultAsync(token);
            },
            ct: ct);
    }

    public async Task<IReadOnlyList<ContactSummary>> GetByIdsAsync(
        IEnumerable<Guid> contactIds, CancellationToken ct = default)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var ids = contactIds.Select(Domain.ValueObjects.ContactId.From).ToList();

        return await dbContext.Contacts
            .Where(c => ids.Contains(c.Id) && c.TenantId == tenantId)
            .AsNoTracking()
            .Select(c => new ContactSummary(
                c.Id.Value, c.DisplayName, c.Email, c.Phone,
                c.Type.ToString(), c.Status.ToString()))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ContactSummary>> SearchAsync(
        string query, int maxResults = 10, CancellationToken ct = default)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var searchTerm = query.Trim().ToLowerInvariant();

        return await dbContext.Contacts
            .Where(c => c.TenantId == tenantId &&
                (c.DisplayName.ToLower().Contains(searchTerm) ||
                 (c.Email != null && c.Email.ToLower().Contains(searchTerm))))
            .AsNoTracking()
            .OrderBy(c => c.DisplayName)
            .Take(maxResults)
            .Select(c => new ContactSummary(
                c.Id.Value, c.DisplayName, c.Email, c.Phone,
                c.Type.ToString(), c.Status.ToString()))
            .ToListAsync(ct);
    }
}
