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

/// <summary>Command to update an existing contact address.</summary>
public sealed record UpdateContactAddressCommand(
    Guid ContactId,
    Guid AddressId,
    string Type,
    string Street1,
    string City,
    string CountryCode,
    string? Street2 = null,
    string? State = null,
    string? PostalCode = null) : ICommand<ContactAddressDto>;

/// <summary>Validates address update input.</summary>
public sealed class UpdateContactAddressValidator : AbstractValidator<UpdateContactAddressCommand>
{
    public UpdateContactAddressValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("lockey_contacts_validation_contact_id_required");

        RuleFor(x => x.AddressId)
            .NotEmpty().WithMessage("lockey_contacts_validation_address_id_required");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("lockey_contacts_validation_address_type_required")
            .Must(t => Enum.TryParse<AddressType>(t, out _))
            .WithMessage("lockey_contacts_validation_address_type_invalid");

        RuleFor(x => x.Street1)
            .NotEmpty().WithMessage("lockey_contacts_validation_street_required")
            .MaximumLength(200).WithMessage("lockey_contacts_validation_street_max_length");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("lockey_contacts_validation_city_required")
            .MaximumLength(100).WithMessage("lockey_contacts_validation_city_max_length");

        RuleFor(x => x.CountryCode)
            .NotEmpty().WithMessage("lockey_contacts_validation_country_code_required")
            .Length(2).WithMessage("lockey_contacts_validation_country_code_length");
    }
}

/// <summary>Updates an existing contact address.</summary>
public sealed class UpdateContactAddressHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<UpdateContactAddressHandler> logger) : ICommandHandler<UpdateContactAddressCommand, ContactAddressDto>
{
    public async Task<Result<ContactAddressDto>> Handle(
        UpdateContactAddressCommand request,
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
            return Result<ContactAddressDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var address = await dbContext.ContactAddresses.FirstOrDefaultAsync(
            a => a.Id == addressId && a.ContactId == contactId,
            cancellationToken);

        if (address is null)
        {
            logger.LogWarning("Address {AddressId} not found for contact {ContactId}", request.AddressId, request.ContactId);
            return Result<ContactAddressDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_address_not_found"));
        }

        var addressType = Enum.Parse<AddressType>(request.Type);
        address.Update(addressType, request.Street1, request.City, request.CountryCode,
            request.Street2, request.State, request.PostalCode);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Address {AddressId} updated for contact {ContactId}", address.Id, contactId);

        return Result<ContactAddressDto>.Success(MapToDto(address),
            LocalizedMessage.Of("lockey_contacts_address_updated"));
    }

    private static ContactAddressDto MapToDto(ContactAddress a) => new(
        a.Id.Value, a.Type.ToString(), a.Street1, a.Street2, a.City,
        a.State, a.PostalCode, a.CountryCode, a.IsPrimary);
}
