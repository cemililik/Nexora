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

/// <summary>Command to set (upsert) a custom field value on a contact.</summary>
public sealed record SetContactCustomFieldCommand(
    Guid ContactId,
    Guid FieldDefinitionId,
    string? Value) : ICommand<ContactCustomFieldDto>;

/// <summary>Validates custom field value setting input.</summary>
public sealed class SetContactCustomFieldValidator : AbstractValidator<SetContactCustomFieldCommand>
{
    public SetContactCustomFieldValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("lockey_contacts_validation_contact_id_required");

        RuleFor(x => x.FieldDefinitionId)
            .NotEmpty().WithMessage("lockey_contacts_validation_field_definition_id_required");
    }
}

/// <summary>Sets or updates a custom field value on a contact.</summary>
public sealed class SetContactCustomFieldHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<SetContactCustomFieldHandler> logger) : ICommandHandler<SetContactCustomFieldCommand, ContactCustomFieldDto>
{
    public async Task<Result<ContactCustomFieldDto>> Handle(
        SetContactCustomFieldCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var contactId = ContactId.From(request.ContactId);
        var definitionId = CustomFieldDefinitionId.From(request.FieldDefinitionId);

        var contact = await dbContext.Contacts.FirstOrDefaultAsync(
            c => c.Id == contactId && c.TenantId == tenantId,
            cancellationToken);

        if (contact is null)
        {
            logger.LogWarning("Contact {ContactId} not found for tenant {TenantId}", request.ContactId, tenantId);
            return Result<ContactCustomFieldDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var definition = await dbContext.CustomFieldDefinitions.FirstOrDefaultAsync(
            d => d.Id == definitionId && d.TenantId == tenantId && d.IsActive,
            cancellationToken);

        if (definition is null)
        {
            logger.LogWarning("Custom field definition {DefinitionId} not found for tenant {TenantId}",
                request.FieldDefinitionId, tenantId);
            return Result<ContactCustomFieldDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_custom_field_definition_not_found"));
        }

        if (definition.IsRequired && string.IsNullOrWhiteSpace(request.Value))
        {
            logger.LogWarning("Required custom field {FieldName} cannot be empty", definition.FieldName);
            return Result<ContactCustomFieldDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_custom_field_value_required"));
        }

        var existing = await dbContext.ContactCustomFields.FirstOrDefaultAsync(
            f => f.ContactId == contactId && f.FieldDefinitionId == definitionId,
            cancellationToken);

        if (existing is not null)
        {
            existing.UpdateValue(request.Value);
        }
        else
        {
            existing = ContactCustomField.Create(contactId, definitionId, request.Value);
            await dbContext.ContactCustomFields.AddAsync(existing, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Custom field {FieldName} set for contact {ContactId}",
            definition.FieldName, contactId);

        var dto = new ContactCustomFieldDto(
            existing.Id.Value, existing.ContactId.Value, existing.FieldDefinitionId.Value,
            definition.FieldName, definition.FieldType, existing.Value);

        return Result<ContactCustomFieldDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_custom_field_value_set"));
    }
}
