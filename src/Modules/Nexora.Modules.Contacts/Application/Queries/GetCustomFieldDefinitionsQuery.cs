using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Queries;

/// <summary>Query to retrieve custom field definitions for a tenant.</summary>
public sealed record GetCustomFieldDefinitionsQuery(bool? IsActive = null) : IQuery<IReadOnlyList<CustomFieldDefinitionDto>>;

/// <summary>Handles custom field definition retrieval for a tenant.</summary>
public sealed class GetCustomFieldDefinitionsHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetCustomFieldDefinitionsHandler> logger) : IQueryHandler<GetCustomFieldDefinitionsQuery, IReadOnlyList<CustomFieldDefinitionDto>>
{
    public async Task<Result<IReadOnlyList<CustomFieldDefinitionDto>>> Handle(
        GetCustomFieldDefinitionsQuery request,
        CancellationToken cancellationToken)
    {
        _ = logger; // Required by observability standards for future slow-query logging
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);

        var query = dbContext.CustomFieldDefinitions
            .Where(d => d.TenantId == tenantId)
            .AsNoTracking();

        if (request.IsActive.HasValue)
            query = query.Where(d => d.IsActive == request.IsActive.Value);

        var definitions = await query
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.FieldName)
            .Select(d => new CustomFieldDefinitionDto(
                d.Id.Value, d.FieldName, d.FieldType, d.Options,
                d.IsRequired, d.DisplayOrder, d.IsActive, d.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<CustomFieldDefinitionDto>>.Success(definitions);
    }
}
