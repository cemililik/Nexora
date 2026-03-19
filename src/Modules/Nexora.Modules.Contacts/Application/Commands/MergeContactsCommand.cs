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

/// <summary>Command to merge a secondary contact into a primary contact.</summary>
public sealed record MergeContactsCommand(
    Guid PrimaryContactId,
    Guid SecondaryContactId,
    bool UseSecondaryEmail = false,
    bool UseSecondaryPhone = false) : ICommand<MergeResultDto>;

/// <summary>Validates merge contacts input.</summary>
public sealed class MergeContactsValidator : AbstractValidator<MergeContactsCommand>
{
    public MergeContactsValidator()
    {
        RuleFor(x => x.PrimaryContactId)
            .NotEmpty().WithMessage("lockey_contacts_validation_primary_contact_id_required");

        RuleFor(x => x.SecondaryContactId)
            .NotEmpty().WithMessage("lockey_contacts_validation_secondary_contact_id_required");

        RuleFor(x => x)
            .Must(x => x.PrimaryContactId != x.SecondaryContactId)
            .WithMessage("lockey_contacts_validation_cannot_merge_same_contact");
    }
}

/// <summary>Merges a secondary contact into a primary contact.</summary>
public sealed class MergeContactsHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<MergeContactsHandler> logger) : ICommandHandler<MergeContactsCommand, MergeResultDto>
{
    public async Task<Result<MergeResultDto>> Handle(
        MergeContactsCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var primaryId = ContactId.From(request.PrimaryContactId);
        var secondaryId = ContactId.From(request.SecondaryContactId);

        var primary = await dbContext.Contacts.FirstOrDefaultAsync(
            c => c.Id == primaryId && c.TenantId == tenantId && c.Status == ContactStatus.Active,
            cancellationToken);

        if (primary is null)
        {
            logger.LogWarning("Primary contact {PrimaryContactId} not found or not active for tenant {TenantId}",
                request.PrimaryContactId, tenantId);
            return Result<MergeResultDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_primary_contact_not_found"));
        }

        var secondary = await dbContext.Contacts.FirstOrDefaultAsync(
            c => c.Id == secondaryId && c.TenantId == tenantId && c.Status == ContactStatus.Active,
            cancellationToken);

        if (secondary is null)
        {
            logger.LogWarning("Secondary contact {SecondaryContactId} not found or not active for tenant {TenantId}",
                request.SecondaryContactId, tenantId);
            return Result<MergeResultDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_secondary_contact_not_found"));
        }

        var mergeService = new ContactMergeService(dbContext);
        var fieldSelections = new MergeFieldSelections(request.UseSecondaryEmail, request.UseSecondaryPhone);

        await mergeService.MergeAsync(primary, secondary, fieldSelections, cancellationToken);

        logger.LogInformation("Contact {SecondaryContactId} merged into {PrimaryContactId} for tenant {TenantId}",
            secondaryId, primaryId, tenantId);

        var dto = new MergeResultDto(
            primary.Id.Value, secondary.Id.Value, primary.DisplayName);

        return Result<MergeResultDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_merge_completed"));
    }
}
