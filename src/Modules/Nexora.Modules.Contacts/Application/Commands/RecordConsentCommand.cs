using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>Command to record a consent grant or revocation for a contact.</summary>
public sealed record RecordConsentCommand(
    Guid ContactId,
    string ConsentType,
    bool Granted,
    string? Source = null,
    string? IpAddress = null) : ICommand<ConsentRecordDto>;

/// <summary>Validates consent recording input.</summary>
public sealed class RecordConsentValidator : AbstractValidator<RecordConsentCommand>
{
    public RecordConsentValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("lockey_contacts_validation_contact_id_required");

        RuleFor(x => x.ConsentType)
            .NotEmpty().WithMessage("lockey_contacts_validation_consent_type_required")
            .Must(t => Enum.TryParse<ConsentType>(t, out _))
            .WithMessage("lockey_contacts_validation_consent_type_invalid");

        RuleFor(x => x.Source)
            .MaximumLength(200).WithMessage("lockey_contacts_validation_consent_source_max_length");
    }
}

/// <summary>Records a consent entry for a contact.</summary>
public sealed class RecordConsentHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<RecordConsentHandler> logger) : ICommandHandler<RecordConsentCommand, ConsentRecordDto>
{
    public async Task<Result<ConsentRecordDto>> Handle(
        RecordConsentCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var contactId = ContactId.From(request.ContactId);

        var contact = await dbContext.Contacts.FirstOrDefaultAsync(
            c => c.Id == contactId && c.TenantId == tenantId,
            cancellationToken);

        if (contact is null)
        {
            logger.LogWarning("Contact {ContactId} not found for tenant {TenantId}", request.ContactId, tenantId);
            return Result<ConsentRecordDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var consentType = Enum.Parse<ConsentType>(request.ConsentType);

        // If revoking, find and revoke the latest active grant
        if (!request.Granted)
        {
            var activeConsent = await dbContext.ConsentRecords
                .Where(c => c.ContactId == contactId && c.ConsentType == consentType && c.Granted && c.RevokedAt == null)
                .OrderByDescending(c => c.GrantedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeConsent is not null)
            {
                activeConsent.Revoke();
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Consent {ConsentType} revoked for contact {ContactId}",
                    consentType, contactId);

                var revokedDto = new ConsentRecordDto(
                    activeConsent.Id.Value, activeConsent.ContactId.Value,
                    activeConsent.ConsentType.ToString(), activeConsent.Granted,
                    activeConsent.Source, activeConsent.GrantedAt, activeConsent.RevokedAt);

                return Result<ConsentRecordDto>.Success(revokedDto,
                    LocalizedMessage.Of("lockey_contacts_consent_revoked"));
            }

            logger.LogWarning("No active consent {ConsentType} found to revoke for contact {ContactId}",
                consentType, contactId);
            return Result<ConsentRecordDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_no_active_consent"));
        }

        var record = ConsentRecord.Create(contactId, consentType, true, request.Source, request.IpAddress);

        await dbContext.ConsentRecords.AddAsync(record, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Consent {ConsentType} granted for contact {ContactId}", consentType, contactId);

        var dto = new ConsentRecordDto(
            record.Id.Value, record.ContactId.Value,
            record.ConsentType.ToString(), record.Granted,
            record.Source, record.GrantedAt, record.RevokedAt);

        return Result<ConsentRecordDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_consent_granted"));
    }
}
