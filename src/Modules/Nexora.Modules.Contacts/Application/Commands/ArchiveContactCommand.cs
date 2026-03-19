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

/// <summary>Command to archive (soft-delete) a contact.</summary>
public sealed record ArchiveContactCommand(Guid ContactId) : ICommand;

/// <summary>Archives a contact by setting its status to Archived.</summary>
public sealed class ArchiveContactHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<ArchiveContactHandler> logger) : ICommandHandler<ArchiveContactCommand>
{
    public async Task<Result> Handle(
        ArchiveContactCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var contactId = ContactId.From(request.ContactId);

        var contact = await dbContext.Contacts
            .FirstOrDefaultAsync(c => c.Id == contactId && c.TenantId == tenantId, cancellationToken);

        if (contact is null)
        {
            logger.LogWarning("Contact archive failed: contact {ContactId} not found for tenant {TenantId}", request.ContactId, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        if (contact.Status == ContactStatus.Archived)
        {
            logger.LogWarning("Contact archive failed: contact {ContactId} already archived", request.ContactId);
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_already_archived"));
        }

        contact.Archive();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Contact {ContactId} archived for tenant {TenantId}", contact.Id, tenantId);

        return Result.Success(LocalizedMessage.Of("lockey_contacts_contact_archived"));
    }
}

/// <summary>Validates archive contact input.</summary>
public sealed class ArchiveContactCommandValidator : AbstractValidator<ArchiveContactCommand>
{
    public ArchiveContactCommandValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty().WithMessage("lockey_validation_required");
    }
}
