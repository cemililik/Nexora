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

/// <summary>Command to add an address to a contact.</summary>
public sealed record AddContactAddressCommand(
    Guid ContactId,
    string Type,
    string Street1,
    string City,
    string CountryCode,
    string? Street2 = null,
    string? State = null,
    string? PostalCode = null,
    bool IsPrimary = false) : ICommand<ContactAddressDto>;

/// <summary>Validates address creation input.</summary>
public sealed class AddContactAddressValidator : AbstractValidator<AddContactAddressCommand>
{
    public AddContactAddressValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("lockey_contacts_validation_contact_id_required");

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

        RuleFor(x => x.Street2)
            .MaximumLength(200).WithMessage("lockey_contacts_validation_street_max_length");

        RuleFor(x => x.State)
            .MaximumLength(100).WithMessage("lockey_contacts_validation_state_max_length");

        RuleFor(x => x.PostalCode)
            .MaximumLength(20).WithMessage("lockey_contacts_validation_postal_code_max_length");
    }
}

/// <summary>Adds an address to a contact.</summary>
public sealed class AddContactAddressHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<AddContactAddressHandler> logger) : ICommandHandler<AddContactAddressCommand, ContactAddressDto>
{
    public async Task<Result<ContactAddressDto>> Handle(
        AddContactAddressCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var contactId = ContactId.From(request.ContactId);

        var contact = await dbContext.Contacts.FirstOrDefaultAsync(
            c => c.Id == contactId && c.TenantId == tenantId,
            cancellationToken);

        if (contact is null)
        {
            logger.LogWarning("Contact {ContactId} not found for tenant {TenantId}", request.ContactId, tenantId);
            return Result<ContactAddressDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var addressType = Enum.Parse<AddressType>(request.Type);

        if (request.IsPrimary)
        {
            var existingPrimary = await dbContext.ContactAddresses
                .Where(a => a.ContactId == contactId && a.IsPrimary)
                .ToListAsync(cancellationToken);
            foreach (var addr in existingPrimary)
                addr.SetPrimary(false);
        }

        var address = ContactAddress.Create(
            contactId, addressType, request.Street1, request.City, request.CountryCode,
            request.Street2, request.State, request.PostalCode, request.IsPrimary);

        await dbContext.ContactAddresses.AddAsync(address, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Address {AddressId} added to contact {ContactId}", address.Id, contactId);

        return Result<ContactAddressDto>.Success(MapToDto(address),
            LocalizedMessage.Of("lockey_contacts_address_added"));
    }

    private static ContactAddressDto MapToDto(ContactAddress a) => new(
        a.Id.Value, a.Type.ToString(), a.Street1, a.Street2, a.City,
        a.State, a.PostalCode, a.CountryCode, a.IsPrimary);
}
