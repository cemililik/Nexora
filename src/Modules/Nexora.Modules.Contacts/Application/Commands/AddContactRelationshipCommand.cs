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

/// <summary>Command to add a relationship between two contacts.</summary>
public sealed record AddContactRelationshipCommand(
    Guid ContactId,
    Guid RelatedContactId,
    string Type) : ICommand<ContactRelationshipDto>;

/// <summary>Validates relationship creation input.</summary>
public sealed class AddContactRelationshipValidator : AbstractValidator<AddContactRelationshipCommand>
{
    public AddContactRelationshipValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("lockey_contacts_validation_contact_id_required");

        RuleFor(x => x.RelatedContactId)
            .NotEmpty().WithMessage("lockey_contacts_validation_related_contact_id_required");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("lockey_contacts_validation_relationship_type_required")
            .Must(t => Enum.TryParse<RelationshipType>(t, out _))
            .WithMessage("lockey_contacts_validation_relationship_type_invalid");

        RuleFor(x => x)
            .Must(x => x.ContactId != x.RelatedContactId)
            .WithMessage("lockey_contacts_validation_self_relationship_not_allowed");
    }
}

/// <summary>Creates a relationship between two contacts.</summary>
public sealed class AddContactRelationshipHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<AddContactRelationshipHandler> logger) : ICommandHandler<AddContactRelationshipCommand, ContactRelationshipDto>
{
    public async Task<Result<ContactRelationshipDto>> Handle(
        AddContactRelationshipCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var contactId = ContactId.From(request.ContactId);
        var relatedId = ContactId.From(request.RelatedContactId);

        var contact = await dbContext.Contacts.FirstOrDefaultAsync(
            c => c.Id == contactId && c.TenantId == tenantId,
            cancellationToken);

        if (contact is null)
        {
            logger.LogWarning("Contact {ContactId} not found for tenant {TenantId}", request.ContactId, tenantId);
            return Result<ContactRelationshipDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var relatedContact = await dbContext.Contacts.FirstOrDefaultAsync(
            c => c.Id == relatedId && c.TenantId == tenantId,
            cancellationToken);

        if (relatedContact is null)
        {
            logger.LogWarning("Related contact {RelatedContactId} not found for tenant {TenantId}", request.RelatedContactId, tenantId);
            return Result<ContactRelationshipDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_related_contact_not_found"));
        }

        var relationshipType = Enum.Parse<RelationshipType>(request.Type);

        var exists = await dbContext.ContactRelationships.AnyAsync(
            r => r.ContactId == contactId && r.RelatedContactId == relatedId && r.Type == relationshipType,
            cancellationToken);

        if (exists)
        {
            logger.LogWarning("Relationship {Type} already exists between {ContactId} and {RelatedContactId}",
                request.Type, request.ContactId, request.RelatedContactId);
            return Result<ContactRelationshipDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_relationship_already_exists"));
        }

        var relationship = ContactRelationship.Create(contactId, relatedId, relationshipType);

        await dbContext.ContactRelationships.AddAsync(relationship, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Relationship {Type} created between {ContactId} and {RelatedContactId}",
            relationshipType, contactId, relatedId);

        var dto = new ContactRelationshipDto(
            relationship.Id.Value, relationship.ContactId.Value,
            relationship.RelatedContactId.Value, relatedContact.DisplayName,
            relationship.Type.ToString(), relationship.CreatedAt);

        return Result<ContactRelationshipDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_relationship_added"));
    }
}
