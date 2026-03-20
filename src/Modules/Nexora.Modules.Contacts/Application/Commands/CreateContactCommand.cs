using FluentValidation;
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

/// <summary>Command to create a new contact.</summary>
public sealed record CreateContactCommand(
    string Type,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? Email,
    string? Phone,
    string Source,
    string? Title = null) : ICommand<ContactDto>;

/// <summary>Validates contact creation input.</summary>
public sealed class CreateContactValidator : AbstractValidator<CreateContactCommand>
{
    public CreateContactValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("lockey_contacts_validation_type_required")
            .Must(t => t is "Individual" or "Organization")
            .WithMessage("lockey_contacts_validation_type_invalid");

        RuleFor(x => x.Source)
            .NotEmpty().WithMessage("lockey_contacts_validation_source_required");

        RuleFor(x => x.Email)
            .MaximumLength(256).WithMessage("lockey_contacts_validation_email_max_length")
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
            .WithMessage("lockey_contacts_validation_email_format");

        RuleFor(x => x.FirstName)
            .MaximumLength(100).WithMessage("lockey_contacts_validation_first_name_max_length");

        RuleFor(x => x.LastName)
            .MaximumLength(100).WithMessage("lockey_contacts_validation_last_name_max_length");

        RuleFor(x => x.CompanyName)
            .MaximumLength(200).WithMessage("lockey_contacts_validation_company_name_max_length");

        When(x => x.Type == "Individual", () =>
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("lockey_contacts_validation_first_name_required");
            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("lockey_contacts_validation_last_name_required");
        });

        When(x => x.Type == "Organization", () =>
        {
            RuleFor(x => x.CompanyName)
                .NotEmpty().WithMessage("lockey_contacts_validation_company_name_required");
        });
    }
}

/// <summary>Creates a contact and persists it to the database.</summary>
public sealed class CreateContactHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<CreateContactHandler> logger) : ICommandHandler<CreateContactCommand, ContactDto>
{
    public async Task<Result<ContactDto>> Handle(
        CreateContactCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var orgId = Guid.Parse(tenantContextAccessor.Current.OrganizationId!);

        var type = Enum.Parse<ContactType>(request.Type);
        var source = Enum.Parse<ContactSource>(request.Source);

        var contact = Contact.Create(
            tenantId, orgId, type,
            request.FirstName, request.LastName, request.CompanyName,
            request.Email, request.Phone, source, request.Title);

        await dbContext.Contacts.AddAsync(contact, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = MapToDto(contact);

        logger.LogInformation("Contact {ContactId} created for tenant {TenantId}", contact.Id, tenantId);

        return Result<ContactDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_contact_created"));
    }

    private static ContactDto MapToDto(Contact c) => new(
        c.Id.Value, c.Type.ToString(), c.Title,
        c.FirstName, c.LastName, c.DisplayName, c.CompanyName,
        c.Email, c.Phone, c.Source.ToString(), c.Status.ToString(),
        c.CreatedAt);
}
