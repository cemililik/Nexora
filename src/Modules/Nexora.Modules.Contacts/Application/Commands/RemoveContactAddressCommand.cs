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

/// <summary>Command to remove an address from a contact.</summary>
public sealed record RemoveContactAddressCommand(
    Guid ContactId,
    Guid AddressId) : ICommand;

/// <summary>Removes an address from a contact.</summary>
public sealed class RemoveContactAddressHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<RemoveContactAddressHandler> logger) : ICommandHandler<RemoveContactAddressCommand>
{
    public async Task<Result> Handle(
        RemoveContactAddressCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var contactId = ContactId.From(request.ContactId);
        var addressId = ContactAddressId.From(request.AddressId);

        var contact = await dbContext.Contacts.FirstOrDefaultAsync(
            c => c.Id == contactId && c.TenantId == tenantId,
            cancellationToken);

        if (contact is null)
        {
            logger.LogWarning("Contact {ContactId} not found for tenant {TenantId}", request.ContactId, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var address = await dbContext.ContactAddresses.FirstOrDefaultAsync(
            a => a.Id == addressId && a.ContactId == contactId,
            cancellationToken);

        if (address is null)
        {
            logger.LogWarning("Address {AddressId} not found for contact {ContactId}", request.AddressId, request.ContactId);
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_address_not_found"));
        }

        dbContext.ContactAddresses.Remove(address);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Address {AddressId} removed from contact {ContactId}", addressId, contactId);

        return Result.Success(LocalizedMessage.Of("lockey_contacts_address_removed"));
    }
}

/// <summary>Validates remove contact address input.</summary>
public sealed class RemoveContactAddressCommandValidator : AbstractValidator<RemoveContactAddressCommand>
{
    public RemoveContactAddressCommandValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.AddressId).NotEmpty().WithMessage("lockey_validation_required");
    }
}
