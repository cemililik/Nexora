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

/// <summary>Command to log an activity entry on a contact's timeline (append-only).</summary>
public sealed record LogContactActivityCommand(
    Guid ContactId,
    string ModuleSource,
    string ActivityType,
    string Summary,
    string? Details = null) : ICommand<ContactActivityDto>;

/// <summary>Validates activity logging input.</summary>
public sealed class LogContactActivityValidator : AbstractValidator<LogContactActivityCommand>
{
    public LogContactActivityValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("lockey_contacts_validation_contact_id_required");

        RuleFor(x => x.ModuleSource)
            .NotEmpty().WithMessage("lockey_contacts_validation_activity_module_source_required")
            .MaximumLength(100).WithMessage("lockey_contacts_validation_activity_module_source_max_length");

        RuleFor(x => x.ActivityType)
            .NotEmpty().WithMessage("lockey_contacts_validation_activity_type_required")
            .MaximumLength(100).WithMessage("lockey_contacts_validation_activity_type_max_length");

        RuleFor(x => x.Summary)
            .NotEmpty().WithMessage("lockey_contacts_validation_activity_summary_required")
            .MaximumLength(500).WithMessage("lockey_contacts_validation_activity_summary_max_length");

        RuleFor(x => x.Details)
            .MaximumLength(5000).WithMessage("lockey_contacts_validation_activity_details_max_length");
    }
}

/// <summary>Logs an activity entry on a contact's timeline.</summary>
public sealed class LogContactActivityHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<LogContactActivityHandler> logger) : ICommandHandler<LogContactActivityCommand, ContactActivityDto>
{
    public async Task<Result<ContactActivityDto>> Handle(
        LogContactActivityCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var orgId = Guid.Parse(tenantContextAccessor.Current.OrganizationId!);
        var contactId = ContactId.From(request.ContactId);

        var contactExists = await dbContext.Contacts.AnyAsync(
            c => c.Id == contactId && c.TenantId == tenantId,
            cancellationToken);

        if (!contactExists)
        {
            logger.LogWarning("Contact {ContactId} not found for tenant {TenantId}", request.ContactId, tenantId);
            return Result<ContactActivityDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var activity = ContactActivity.Create(
            contactId, orgId, request.ModuleSource, request.ActivityType,
            request.Summary, request.Details);

        await dbContext.ContactActivities.AddAsync(activity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Activity {ActivityType} logged for contact {ContactId} from {ModuleSource}",
            request.ActivityType, contactId, request.ModuleSource);

        var dto = new ContactActivityDto(
            activity.Id.Value, activity.ContactId.Value,
            activity.ModuleSource, activity.ActivityType,
            activity.Summary, activity.Details, activity.OccurredAt);

        return Result<ContactActivityDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_activity_logged"));
    }
}
