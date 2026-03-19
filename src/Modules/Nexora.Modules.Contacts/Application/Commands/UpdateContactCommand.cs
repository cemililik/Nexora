using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>Command to update a contact's information.</summary>
public sealed record UpdateContactCommand(
    Guid ContactId,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? Email,
    string? Phone,
    string? Mobile,
    string? Website,
    string? TaxId,
    string Language,
    string Currency,
    string? Title = null) : ICommand<ContactDto>;

/// <summary>Validates contact update input.</summary>
public sealed class UpdateContactValidator : AbstractValidator<UpdateContactCommand>
{
    public UpdateContactValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("lockey_contacts_validation_contact_id_required");

        RuleFor(x => x.Email)
            .MaximumLength(256).WithMessage("lockey_contacts_validation_email_max_length")
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
            .WithMessage("lockey_contacts_validation_email_format");

        RuleFor(x => x.Language)
            .NotEmpty().WithMessage("lockey_contacts_validation_language_required")
            .MaximumLength(10).WithMessage("lockey_contacts_validation_language_max_length");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("lockey_contacts_validation_currency_required")
            .Length(3).WithMessage("lockey_contacts_validation_currency_length");
    }
}

/// <summary>Updates a contact's properties.</summary>
public sealed class UpdateContactHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<UpdateContactHandler> logger) : ICommandHandler<UpdateContactCommand, ContactDto>
{
    public async Task<Result<ContactDto>> Handle(
        UpdateContactCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var contactId = ContactId.From(request.ContactId);

        var contact = await dbContext.Contacts
            .FirstOrDefaultAsync(c => c.Id == contactId && c.TenantId == tenantId, cancellationToken);

        if (contact is null)
        {
            logger.LogWarning("Contact update failed: contact {ContactId} not found for tenant {TenantId}", request.ContactId, tenantId);
            return Result<ContactDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        contact.Update(
            request.FirstName, request.LastName, request.CompanyName,
            request.Email, request.Phone, request.Mobile,
            request.Website, request.TaxId, request.Language, request.Currency,
            request.Title);

        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = new ContactDto(
            contact.Id.Value, contact.Type.ToString(), contact.Title,
            contact.FirstName, contact.LastName, contact.DisplayName, contact.CompanyName,
            contact.Email, contact.Phone, contact.Source.ToString(), contact.Status.ToString(),
            contact.CreatedAt);

        logger.LogInformation("Contact {ContactId} updated for tenant {TenantId}", contact.Id, tenantId);

        return Result<ContactDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_contact_updated"));
    }
}
