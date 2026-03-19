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

/// <summary>Query to retrieve custom field values for a contact.</summary>
public sealed record GetContactCustomFieldsQuery(Guid ContactId) : IQuery<IReadOnlyList<ContactCustomFieldDto>>;

/// <summary>Handles custom field value retrieval for a contact.</summary>
public sealed class GetContactCustomFieldsHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetContactCustomFieldsHandler> logger) : IQueryHandler<GetContactCustomFieldsQuery, IReadOnlyList<ContactCustomFieldDto>>
{
    public async Task<Result<IReadOnlyList<ContactCustomFieldDto>>> Handle(
        GetContactCustomFieldsQuery request,
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
            return Result<IReadOnlyList<ContactCustomFieldDto>>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var fields = await dbContext.ContactCustomFields
            .Where(f => f.ContactId == contactId)
            .Join(dbContext.CustomFieldDefinitions,
                f => f.FieldDefinitionId,
                d => d.Id,
                (f, d) => new ContactCustomFieldDto(
                    f.Id.Value, f.ContactId.Value, f.FieldDefinitionId.Value,
                    d.FieldName, d.FieldType, f.Value))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ContactCustomFieldDto>>.Success(fields);
    }
}
