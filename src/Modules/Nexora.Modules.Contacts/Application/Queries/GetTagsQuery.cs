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

/// <summary>Query to retrieve tags with optional category filter.</summary>
public sealed record GetTagsQuery(
    string? Category = null,
    bool? IsActive = null) : IQuery<IReadOnlyList<TagDto>>;

/// <summary>Handles tag list retrieval with optional filtering.</summary>
public sealed class GetTagsHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetTagsHandler> logger) : IQueryHandler<GetTagsQuery, IReadOnlyList<TagDto>>
{
    public async Task<Result<IReadOnlyList<TagDto>>> Handle(
        GetTagsQuery request,
        CancellationToken cancellationToken)
    {
        _ = logger; // Required by observability standards for future slow-query logging
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);

        var query = dbContext.Tags
            .Where(t => t.TenantId == tenantId)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Category) &&
            Enum.TryParse<TagCategory>(request.Category, out var category))
        {
            query = query.Where(t => t.Category == category);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(t => t.IsActive == request.IsActive.Value);
        }

        var tags = await query
            .OrderBy(t => t.Name)
            .Select(t => new TagDto(
                t.Id.Value, t.Name, t.Category.ToString(), t.Color, t.IsActive, t.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<TagDto>>.Success(tags);
    }
}
