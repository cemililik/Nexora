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

/// <summary>Command to restore an archived contact.</summary>
public sealed record RestoreContactCommand(Guid ContactId) : ICommand;

/// <summary>Restores a contact from Archived status back to Active.</summary>
public sealed class RestoreContactHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<RestoreContactHandler> logger) : ICommandHandler<RestoreContactCommand>
{
    public async Task<Result> Handle(
        RestoreContactCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var contactId = ContactId.From(request.ContactId);

        var contact = await dbContext.Contacts
            .FirstOrDefaultAsync(c => c.Id == contactId && c.TenantId == tenantId, cancellationToken);

        if (contact is null)
        {
            logger.LogWarning("Contact restore failed: contact {ContactId} not found for tenant {TenantId}", request.ContactId, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        if (contact.Status != ContactStatus.Archived)
        {
            logger.LogWarning("Contact restore failed: contact {ContactId} is not archived (status: {Status})", request.ContactId, contact.Status);
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_archived"));
        }

        contact.Restore();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Contact {ContactId} restored for tenant {TenantId}", contact.Id, tenantId);

        return Result.Success(LocalizedMessage.Of("lockey_contacts_contact_restored"));
    }
}

/// <summary>Validates restore contact input.</summary>
public sealed class RestoreContactCommandValidator : AbstractValidator<RestoreContactCommand>
{
    public RestoreContactCommandValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty().WithMessage("lockey_validation_required");
    }
}
