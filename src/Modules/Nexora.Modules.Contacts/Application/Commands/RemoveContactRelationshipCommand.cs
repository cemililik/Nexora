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

/// <summary>Command to remove a relationship between two contacts.</summary>
public sealed record RemoveContactRelationshipCommand(
    Guid ContactId,
    Guid RelationshipId) : ICommand;

/// <summary>Removes a relationship from a contact.</summary>
public sealed class RemoveContactRelationshipHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<RemoveContactRelationshipHandler> logger) : ICommandHandler<RemoveContactRelationshipCommand>
{
    public async Task<Result> Handle(
        RemoveContactRelationshipCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var contactId = ContactId.From(request.ContactId);
        var relationshipId = ContactRelationshipId.From(request.RelationshipId);

        var contact = await dbContext.Contacts.FirstOrDefaultAsync(
            c => c.Id == contactId && c.TenantId == tenantId,
            cancellationToken);

        if (contact is null)
        {
            logger.LogWarning("Contact {ContactId} not found for tenant {TenantId}", request.ContactId, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var relationship = await dbContext.ContactRelationships.FirstOrDefaultAsync(
            r => r.Id == relationshipId && r.ContactId == contactId,
            cancellationToken);

        if (relationship is null)
        {
            logger.LogWarning("Relationship {RelationshipId} not found for contact {ContactId}",
                request.RelationshipId, request.ContactId);
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_relationship_not_found"));
        }

        dbContext.ContactRelationships.Remove(relationship);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Relationship {RelationshipId} removed from contact {ContactId}",
            relationshipId, contactId);

        return Result.Success(LocalizedMessage.Of("lockey_contacts_relationship_removed"));
    }
}

/// <summary>Validates remove contact relationship input.</summary>
public sealed class RemoveContactRelationshipCommandValidator : AbstractValidator<RemoveContactRelationshipCommand>
{
    public RemoveContactRelationshipCommandValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.RelationshipId).NotEmpty().WithMessage("lockey_validation_required");
    }
}
