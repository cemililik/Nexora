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

/// <summary>Query to retrieve activity timeline for a contact.</summary>
public sealed record GetContactActivitiesQuery(
    Guid ContactId,
    string? ModuleSource = null,
    int? Take = null) : IQuery<IReadOnlyList<ContactActivityDto>>;

/// <summary>Handles activity timeline retrieval for a contact.</summary>
public sealed class GetContactActivitiesHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetContactActivitiesHandler> logger) : IQueryHandler<GetContactActivitiesQuery, IReadOnlyList<ContactActivityDto>>
{
    public async Task<Result<IReadOnlyList<ContactActivityDto>>> Handle(
        GetContactActivitiesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var contactId = ContactId.From(request.ContactId);

        var contactExists = await dbContext.Contacts.AnyAsync(
            c => c.Id == contactId && c.TenantId == tenantId,
            cancellationToken);

        if (!contactExists)
        {
            logger.LogDebug("Contact {ContactId} not found", request.ContactId);
            return Result<IReadOnlyList<ContactActivityDto>>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var query = dbContext.ContactActivities
            .Where(a => a.ContactId == contactId)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.ModuleSource))
            query = query.Where(a => a.ModuleSource == request.ModuleSource);

        query = query.OrderByDescending(a => a.OccurredAt);

        if (request.Take.HasValue)
            query = query.Take(request.Take.Value);

        var activities = await query
            .Select(a => new ContactActivityDto(
                a.Id.Value, a.ContactId.Value, a.ModuleSource,
                a.ActivityType, a.Summary, a.Details, a.OccurredAt))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ContactActivityDto>>.Success(activities);
    }
}
