using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Queries;

/// <summary>Paginated query to list contacts with optional filtering.</summary>
public sealed record GetContactsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Status = null,
    string? Type = null,
    Guid? TagId = null) : IQuery<PagedResult<ContactDto>>;

/// <summary>Returns a paginated, filterable list of contacts for the current tenant.</summary>
public sealed class GetContactsHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetContactsHandler> logger) : IQueryHandler<GetContactsQuery, PagedResult<ContactDto>>
{
    public async Task<Result<PagedResult<ContactDto>>> Handle(
        GetContactsQuery request,
        CancellationToken cancellationToken)
    {
        _ = logger; // Required by observability standards for future slow-query logging
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);

        var query = dbContext.Contacts
            .Where(c => c.TenantId == tenantId)
            .AsQueryable();

        // Filter by status
        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<ContactStatus>(request.Status, out var status))
            query = query.Where(c => c.Status == status);

        // Filter by type
        if (!string.IsNullOrEmpty(request.Type) && Enum.TryParse<ContactType>(request.Type, out var type))
            query = query.Where(c => c.Type == type);

        // Search by name or email
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLowerInvariant();
            query = query.Where(c =>
                c.DisplayName.ToLower().Contains(search) ||
                (c.Email != null && c.Email.Contains(search)));
        }

        // Filter by tag
        if (request.TagId.HasValue)
        {
            var tagId = TagId.From(request.TagId.Value);
            query = query.Where(c => c.Tags.Any(t => t.TagId == tagId));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.DisplayName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new ContactDto(
                c.Id.Value, c.Type.ToString(), c.Title,
                c.FirstName, c.LastName, c.DisplayName, c.CompanyName,
                c.Email, c.Phone, c.Source.ToString(), c.Status.ToString(),
                c.CreatedAt))
            .ToListAsync(cancellationToken);

        var result = new PagedResult<ContactDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<ContactDto>>.Success(result,
            LocalizedMessage.Of("lockey_contacts_contacts_listed"));
    }
}
