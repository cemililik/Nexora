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

/// <summary>Command to assign a tag to a contact.</summary>
public sealed record AddTagToContactCommand(
    Guid ContactId,
    Guid TagId) : ICommand<ContactTagDto>;

/// <summary>Validates tag assignment input.</summary>
public sealed class AddTagToContactValidator : AbstractValidator<AddTagToContactCommand>
{
    public AddTagToContactValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("lockey_contacts_validation_contact_id_required");

        RuleFor(x => x.TagId)
            .NotEmpty().WithMessage("lockey_contacts_validation_tag_id_required");
    }
}

/// <summary>Assigns a tag to a contact within the current organization.</summary>
public sealed class AddTagToContactHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<AddTagToContactHandler> logger) : ICommandHandler<AddTagToContactCommand, ContactTagDto>
{
    public async Task<Result<ContactTagDto>> Handle(
        AddTagToContactCommand request,
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
            return Result<ContactTagDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var tag = await dbContext.Tags.FirstOrDefaultAsync(
            t => t.Id == tagId && t.TenantId == tenantId && t.IsActive,
            cancellationToken);

        if (tag is null)
        {
            logger.LogWarning("Tag {TagId} not found or inactive for tenant {TenantId}", request.TagId, tenantId);
            return Result<ContactTagDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_tag_not_found"));
        }

        var alreadyAssigned = await dbContext.ContactTags.AnyAsync(
            ct => ct.ContactId == contactId && ct.TagId == tagId && ct.OrganizationId == orgId,
            cancellationToken);

        if (alreadyAssigned)
        {
            logger.LogWarning("Tag {TagId} already assigned to contact {ContactId}", request.TagId, request.ContactId);
            return Result<ContactTagDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_tag_already_assigned"));
        }

        var contactTag = ContactTag.Create(contactId, tagId, orgId);

        await dbContext.ContactTags.AddAsync(contactTag, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Tag {TagId} assigned to contact {ContactId} in org {OrgId}", tagId, contactId, orgId);

        var dto = new ContactTagDto(
            contactTag.Id.Value, contactTag.ContactId.Value, contactTag.TagId.Value,
            tag.Name, tag.Category.ToString(), tag.Color, contactTag.AssignedAt);

        return Result<ContactTagDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_tag_assigned"));
    }
}
