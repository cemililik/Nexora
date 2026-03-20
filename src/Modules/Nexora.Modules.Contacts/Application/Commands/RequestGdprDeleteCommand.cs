using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>Command to anonymize and archive a contact's personal data (GDPR right to erasure).</summary>
public sealed record RequestGdprDeleteCommand(Guid ContactId, string Reason) : ICommand;

/// <summary>Anonymizes PII, revokes all consents, and archives the contact.</summary>
public sealed class RequestGdprDeleteHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<RequestGdprDeleteHandler> logger) : ICommandHandler<RequestGdprDeleteCommand>
{
    private const string AnonymizedPlaceholder = "[REDACTED]";
    public async Task<Result> Handle(
        RequestGdprDeleteCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var contactId = ContactId.From(request.ContactId);

        var contact = await dbContext.Contacts.FirstOrDefaultAsync(
            c => c.Id == contactId && c.TenantId == tenantId,
            cancellationToken);

        if (contact is null)
        {
            logger.LogWarning("GDPR delete requested for non-existent contact {ContactId}", request.ContactId);
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        if (contact.Status == ContactStatus.Merged)
        {
            logger.LogWarning("GDPR delete not allowed for merged contact {ContactId}", request.ContactId);
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_gdpr_delete_merged_contact"));
        }

        // Anonymize PII
        contact.Update(
            firstName: AnonymizedPlaceholder,
            lastName: AnonymizedPlaceholder,
            companyName: null,
            email: null,
            phone: null,
            mobile: null,
            website: null,
            taxId: null,
            language: contact.Language,
            currency: contact.Currency,
            title: null);

        // Archive the contact
        if (contact.Status == ContactStatus.Active)
            contact.Archive();

        // Revoke all active consents
        var activeConsents = await dbContext.ConsentRecords
            .Where(c => c.ContactId == contactId && c.Granted && c.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var consent in activeConsents)
            consent.Revoke();

        // Remove addresses
        var addresses = await dbContext.ContactAddresses
            .Where(a => a.ContactId == contactId)
            .ToListAsync(cancellationToken);
        dbContext.ContactAddresses.RemoveRange(addresses);

        // Remove notes (contain PII)
        var notes = await dbContext.ContactNotes
            .Where(n => n.ContactId == contactId)
            .ToListAsync(cancellationToken);
        dbContext.ContactNotes.RemoveRange(notes);

        // Remove custom fields (may contain PII)
        var customFields = await dbContext.ContactCustomFields
            .Where(f => f.ContactId == contactId)
            .ToListAsync(cancellationToken);
        dbContext.ContactCustomFields.RemoveRange(customFields);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("GDPR delete completed for contact {ContactId}. Reason: {Reason}",
            contactId, request.Reason);

        return Result.Success(LocalizedMessage.Of("lockey_contacts_gdpr_delete_completed"));
    }
}

/// <summary>Validates GDPR delete request input.</summary>
public sealed class RequestGdprDeleteCommandValidator : AbstractValidator<RequestGdprDeleteCommand>
{
    public RequestGdprDeleteCommandValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.Reason).MaximumLength(500).WithMessage("lockey_validation_max_length");
    }
}
