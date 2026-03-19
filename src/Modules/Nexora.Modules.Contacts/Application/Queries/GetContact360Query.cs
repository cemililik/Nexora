using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Queries;

/// <summary>Query to retrieve the full 360-degree view for a contact.</summary>
public sealed record GetContact360Query(Guid ContactId) : IQuery<Contact360Dto>;

/// <summary>Aggregates all contact sub-entities and module summaries into a 360-degree view.</summary>
public sealed class GetContact360Handler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ContactActivityContributorAggregator contributorAggregator,
    ILogger<GetContact360Handler> logger) : IQueryHandler<GetContact360Query, Contact360Dto>
{
    public async Task<Result<Contact360Dto>> Handle(
        GetContact360Query request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var orgId = Guid.Parse(tenantContextAccessor.Current.OrganizationId!);
        var contactId = ContactId.From(request.ContactId);

        // Load contact with addresses and tags
        var contact = await dbContext.Contacts
            .Include(c => c.Addresses)
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == contactId && c.TenantId == tenantId, cancellationToken);

        if (contact is null)
        {
            logger.LogDebug("Contact {ContactId} not found", request.ContactId);
            return Result<Contact360Dto>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        // Load tag details
        var tagIds = contact.Tags.Select(t => t.TagId).ToList();
        var tags = await dbContext.Tags
            .Where(t => tagIds.Contains(t.Id))
            .ToListAsync(cancellationToken);

        var contactDetail = new ContactDetailDto(
            contact.Id.Value, contact.Type.ToString(), contact.Title,
            contact.FirstName, contact.LastName, contact.DisplayName, contact.CompanyName,
            contact.Email, contact.Phone, contact.Mobile, contact.Website, contact.TaxId,
            contact.Language, contact.Currency, contact.Source.ToString(), contact.Status.ToString(),
            contact.MergedIntoId?.Value, contact.CreatedAt,
            contact.Addresses.Select(a => new ContactAddressDto(
                a.Id.Value, a.Type.ToString(), a.Street1, a.Street2,
                a.City, a.State, a.PostalCode, a.CountryCode, a.IsPrimary)).ToList(),
            tags.Select(t => new ContactTagSummaryDto(
                t.Id.Value, t.Name, t.Category.ToString(), t.Color)).ToList());

        // Load relationships
        var relationships = await dbContext.ContactRelationships
            .Where(r => r.ContactId == contactId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var relatedIds = relationships.Select(r => r.RelatedContactId).Distinct().ToList();
        var relatedContacts = await dbContext.Contacts
            .Where(c => relatedIds.Contains(c.Id))
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.DisplayName, cancellationToken);

        var relationshipDtos = relationships.Select(r => new ContactRelationshipDto(
            r.Id.Value, r.ContactId.Value, r.RelatedContactId.Value,
            relatedContacts.GetValueOrDefault(r.RelatedContactId, "lockey_common_unknown"),
            r.Type.ToString(), r.CreatedAt)).ToList();

        // Load communication preferences
        var preferences = await dbContext.CommunicationPreferences
            .Where(p => p.ContactId == contactId)
            .AsNoTracking()
            .OrderBy(p => p.Channel)
            .Select(p => new CommunicationPreferenceDto(
                p.Id.Value, p.ContactId.Value, p.Channel.ToString(),
                p.OptedIn, p.OptedInAt, p.OptedOutAt, p.OptInSource))
            .ToListAsync(cancellationToken);

        // Load recent notes (pinned first, max 10)
        var notes = await dbContext.ContactNotes
            .Where(n => n.ContactId == contactId)
            .AsNoTracking()
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedAt)
            .Take(10)
            .Select(n => new ContactNoteDto(
                n.Id.Value, n.ContactId.Value, n.AuthorUserId,
                n.Content, n.IsPinned, n.CreatedAt, n.UpdatedAt))
            .ToListAsync(cancellationToken);

        // Load consent records
        var consents = await dbContext.ConsentRecords
            .Where(c => c.ContactId == contactId)
            .AsNoTracking()
            .OrderByDescending(c => c.GrantedAt)
            .Select(c => new ConsentRecordDto(
                c.Id.Value, c.ContactId.Value, c.ConsentType.ToString(),
                c.Granted, c.Source, c.GrantedAt, c.RevokedAt))
            .ToListAsync(cancellationToken);

        // Load recent activities (max 20)
        var activities = await dbContext.ContactActivities
            .Where(a => a.ContactId == contactId)
            .AsNoTracking()
            .OrderByDescending(a => a.OccurredAt)
            .Take(20)
            .Select(a => new ContactActivityDto(
                a.Id.Value, a.ContactId.Value, a.ModuleSource,
                a.ActivityType, a.Summary, a.Details, a.OccurredAt))
            .ToListAsync(cancellationToken);

        // Load custom fields
        var customFields = await dbContext.ContactCustomFields
            .Where(f => f.ContactId == contactId)
            .Join(dbContext.CustomFieldDefinitions,
                f => f.FieldDefinitionId, d => d.Id,
                (f, d) => new ContactCustomFieldDto(
                    f.Id.Value, f.ContactId.Value, f.FieldDefinitionId.Value,
                    d.FieldName, d.FieldType, f.Value))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Aggregate module summaries from contributors
        var moduleSummaries = await contributorAggregator
            .GetAllSummariesAsync(request.ContactId, orgId, cancellationToken);

        var dto = new Contact360Dto(
            contactDetail, relationshipDtos, preferences, notes,
            consents, activities, customFields, moduleSummaries);

        return Result<Contact360Dto>.Success(dto);
    }
}
