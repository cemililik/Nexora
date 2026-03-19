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

/// <summary>Command to remove a tag from a contact.</summary>
public sealed record RemoveTagFromContactCommand(
    Guid ContactId,
    Guid TagId) : ICommand;

/// <summary>Validates tag removal input.</summary>
public sealed class RemoveTagFromContactValidator : AbstractValidator<RemoveTagFromContactCommand>
{
    public RemoveTagFromContactValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("lockey_contacts_validation_contact_id_required");

        RuleFor(x => x.TagId)
            .NotEmpty().WithMessage("lockey_contacts_validation_tag_id_required");
    }
}

/// <summary>Removes a tag assignment from a contact within the current organization.</summary>
public sealed class RemoveTagFromContactHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<RemoveTagFromContactHandler> logger) : ICommandHandler<RemoveTagFromContactCommand>
{
    public async Task<Result> Handle(
        RemoveTagFromContactCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var orgId = Guid.Parse(tenantContextAccessor.Current.OrganizationId!);
        var contactId = ContactId.From(request.ContactId);
        var tagId = TagId.From(request.TagId);

        var contact = await dbContext.Contacts.FirstOrDefaultAsync(
            c => c.Id == contactId && c.TenantId == tenantId,
            cancellationToken);

        if (contact is null)
        {
            logger.LogWarning("Contact {ContactId} not found for tenant {TenantId}", request.ContactId, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var contactTag = await dbContext.ContactTags.FirstOrDefaultAsync(
            ct => ct.ContactId == contactId && ct.TagId == tagId && ct.OrganizationId == orgId,
            cancellationToken);

        if (contactTag is null)
        {
            logger.LogWarning("Tag {TagId} not assigned to contact {ContactId} in org {OrgId}", request.TagId, request.ContactId, orgId);
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_tag_not_assigned"));
        }

        dbContext.ContactTags.Remove(contactTag);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Tag {TagId} removed from contact {ContactId} in org {OrgId}", tagId, contactId, orgId);

        return Result.Success(
            LocalizedMessage.Of("lockey_contacts_tag_removed"));
    }
}
