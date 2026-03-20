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

/// <summary>Query to retrieve addresses for a contact.</summary>
public sealed record GetContactAddressesQuery(Guid ContactId) : IQuery<IReadOnlyList<ContactAddressDto>>;

/// <summary>Handles address list retrieval for a contact.</summary>
public sealed class GetContactAddressesHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetContactAddressesHandler> logger) : IQueryHandler<GetContactAddressesQuery, IReadOnlyList<ContactAddressDto>>
{
    public async Task<Result<IReadOnlyList<ContactAddressDto>>> Handle(
        GetContactAddressesQuery request,
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
            return Result<IReadOnlyList<ContactAddressDto>>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var addresses = await dbContext.ContactAddresses
            .Where(a => a.ContactId == contactId)
            .AsNoTracking()
            .OrderByDescending(a => a.IsPrimary)
            .ThenBy(a => a.Type)
            .Select(a => new ContactAddressDto(
                a.Id.Value, a.Type.ToString(), a.Street1, a.Street2,
                a.City, a.State, a.PostalCode, a.CountryCode, a.IsPrimary))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ContactAddressDto>>.Success(addresses);
    }
}
