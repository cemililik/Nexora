using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>Command to export all personal data for a contact (GDPR/KVKK compliance).</summary>
public sealed record RequestGdprExportCommand(Guid ContactId) : ICommand<GdprExportDto>;

/// <summary>Compiles all contact data for GDPR export.</summary>
public sealed class RequestGdprExportHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<RequestGdprExportHandler> logger) : ICommandHandler<RequestGdprExportCommand, GdprExportDto>
{
    public async Task<Result<GdprExportDto>> Handle(
        RequestGdprExportCommand request,
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
            logger.LogWarning("GDPR export requested for non-existent contact {ContactId}", request.ContactId);
            return Result<GdprExportDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
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

        var notes = await dbContext.ContactNotes
            .Where(n => n.ContactId == contactId)
            .AsNoTracking()
            .Select(n => new ContactNoteDto(
                n.Id.Value, n.ContactId.Value, n.AuthorUserId,
                n.Content, n.IsPinned, n.CreatedAt, n.UpdatedAt))
            .ToListAsync(cancellationToken);

        var consents = await dbContext.ConsentRecords
            .Where(c => c.ContactId == contactId)
            .AsNoTracking()
            .Select(c => new ConsentRecordDto(
                c.Id.Value, c.ContactId.Value, c.ConsentType.ToString(),
                c.Granted, c.Source, c.GrantedAt, c.RevokedAt))
            .ToListAsync(cancellationToken);

        var activities = await dbContext.ContactActivities
            .Where(a => a.ContactId == contactId)
            .AsNoTracking()
            .Select(a => new ContactActivityDto(
                a.Id.Value, a.ContactId.Value, a.ModuleSource,
                a.ActivityType, a.Summary, a.Details, a.OccurredAt))
            .ToListAsync(cancellationToken);

        var customFields = await dbContext.ContactCustomFields
            .Where(f => f.ContactId == contactId)
            .Join(dbContext.CustomFieldDefinitions,
                f => f.FieldDefinitionId, d => d.Id,
                (f, d) => new ContactCustomFieldDto(
                    f.Id.Value, f.ContactId.Value, f.FieldDefinitionId.Value,
                    d.FieldName, d.FieldType, f.Value))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dto = new GdprExportDto(
            contact.Id.Value, contact.DisplayName, contactDetail,
            notes, consents, activities, customFields, DateTimeOffset.UtcNow);

        logger.LogInformation("GDPR export completed for contact {ContactId}", contactId);

        return Result<GdprExportDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_gdpr_export_completed"));
    }
}

/// <summary>Validates GDPR export request input.</summary>
public sealed class RequestGdprExportCommandValidator : AbstractValidator<RequestGdprExportCommand>
{
    public RequestGdprExportCommandValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty().WithMessage("lockey_validation_required");
    }
}
