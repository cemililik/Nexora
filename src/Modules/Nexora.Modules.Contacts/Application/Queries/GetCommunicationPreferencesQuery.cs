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

/// <summary>Query to retrieve communication preferences for a contact.</summary>
public sealed record GetCommunicationPreferencesQuery(Guid ContactId) : IQuery<IReadOnlyList<CommunicationPreferenceDto>>;

/// <summary>Handles communication preferences retrieval for a contact.</summary>
public sealed class GetCommunicationPreferencesHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetCommunicationPreferencesHandler> logger) : IQueryHandler<GetCommunicationPreferencesQuery, IReadOnlyList<CommunicationPreferenceDto>>
{
    public async Task<Result<IReadOnlyList<CommunicationPreferenceDto>>> Handle(
        GetCommunicationPreferencesQuery request,
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
            return Result<IReadOnlyList<CommunicationPreferenceDto>>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var preferences = await dbContext.CommunicationPreferences
            .Where(p => p.ContactId == contactId)
            .AsNoTracking()
            .OrderBy(p => p.Channel)
            .Select(p => new CommunicationPreferenceDto(
                p.Id.Value, p.ContactId.Value, p.Channel.ToString(),
                p.OptedIn, p.OptedInAt, p.OptedOutAt, p.OptInSource))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<CommunicationPreferenceDto>>.Success(preferences);
    }
}
