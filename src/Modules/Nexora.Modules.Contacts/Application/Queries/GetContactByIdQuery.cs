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

/// <summary>Query to get contact detail by ID, including addresses and tags.</summary>
public sealed record GetContactByIdQuery(Guid ContactId) : IQuery<ContactDetailDto>;

/// <summary>Returns contact detail with addresses and tags.</summary>
public sealed class GetContactByIdHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetContactByIdHandler> logger) : IQueryHandler<GetContactByIdQuery, ContactDetailDto>
{
    public async Task<Result<ContactDetailDto>> Handle(
        GetContactByIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var contactId = ContactId.From(request.ContactId);

        var contact = await dbContext.Contacts
            .Include(c => c.Addresses)
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == contactId && c.TenantId == tenantId, cancellationToken);

        if (contact is null)
        {
            logger.LogDebug("Contact {ContactId} not found", request.ContactId);
            return Result<ContactDetailDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        // Load tag details for the summary
        var tagIds = contact.Tags.Select(t => t.TagId).ToList();
        var tags = await dbContext.Tags
            .Where(t => tagIds.Contains(t.Id))
            .ToListAsync(cancellationToken);

        var addressDtos = contact.Addresses.Select(a => new ContactAddressDto(
            a.Id.Value, a.Type.ToString(), a.Street1, a.Street2,
            a.City, a.State, a.PostalCode, a.CountryCode, a.IsPrimary)).ToList();

        var tagDtos = tags.Select(t => new ContactTagSummaryDto(
            t.Id.Value, t.Name, t.Category.ToString(), t.Color)).ToList();

        var dto = new ContactDetailDto(
            contact.Id.Value, contact.Type.ToString(), contact.Title,
            contact.FirstName, contact.LastName, contact.DisplayName, contact.CompanyName,
            contact.Email, contact.Phone, contact.Mobile, contact.Website, contact.TaxId,
            contact.Language, contact.Currency, contact.Source.ToString(), contact.Status.ToString(),
            contact.MergedIntoId?.Value,
            contact.CreatedAt, addressDtos, tagDtos);

        return Result<ContactDetailDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_contact_retrieved"));
    }
}
