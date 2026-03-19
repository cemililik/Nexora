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

/// <summary>Query to retrieve relationships for a contact.</summary>
public sealed record GetContactRelationshipsQuery(Guid ContactId) : IQuery<IReadOnlyList<ContactRelationshipDto>>;

/// <summary>Handles relationship list retrieval for a contact.</summary>
public sealed class GetContactRelationshipsHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetContactRelationshipsHandler> logger) : IQueryHandler<GetContactRelationshipsQuery, IReadOnlyList<ContactRelationshipDto>>
{
    public async Task<Result<IReadOnlyList<ContactRelationshipDto>>> Handle(
        GetContactRelationshipsQuery request,
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
            return Result<IReadOnlyList<ContactRelationshipDto>>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var relationships = await dbContext.ContactRelationships
            .Where(r => r.ContactId == contactId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var relatedIds = relationships.Select(r => r.RelatedContactId).Distinct().ToList();
        var relatedContacts = await dbContext.Contacts
            .Where(c => relatedIds.Contains(c.Id))
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.DisplayName, cancellationToken);

        var dtos = relationships.Select(r => new ContactRelationshipDto(
            r.Id.Value, r.ContactId.Value, r.RelatedContactId.Value,
            relatedContacts.GetValueOrDefault(r.RelatedContactId, "lockey_common_unknown"),
            r.Type.ToString(), r.CreatedAt))
            .OrderBy(d => d.Type)
            .ToList();

        return Result<IReadOnlyList<ContactRelationshipDto>>.Success(dtos);
    }
}
