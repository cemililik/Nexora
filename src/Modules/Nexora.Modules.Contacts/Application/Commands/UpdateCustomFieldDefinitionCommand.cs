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

/// <summary>Command to update a custom field definition.</summary>
public sealed record UpdateCustomFieldDefinitionCommand(
    Guid DefinitionId,
    string FieldName,
    string? Options,
    bool IsRequired,
    int DisplayOrder) : ICommand<CustomFieldDefinitionDto>;

/// <summary>Validates custom field definition update input.</summary>
public sealed class UpdateCustomFieldDefinitionValidator : AbstractValidator<UpdateCustomFieldDefinitionCommand>
{
    public UpdateCustomFieldDefinitionValidator()
    {
        RuleFor(x => x.DefinitionId)
            .NotEmpty().WithMessage("lockey_contacts_validation_definition_id_required");

        RuleFor(x => x.FieldName)
            .NotEmpty().WithMessage("lockey_contacts_validation_field_name_required")
            .MaximumLength(100).WithMessage("lockey_contacts_validation_field_name_max_length");

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("lockey_contacts_validation_display_order_negative");
    }
}

/// <summary>Updates a custom field definition.</summary>
public sealed class UpdateCustomFieldDefinitionHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<UpdateCustomFieldDefinitionHandler> logger) : ICommandHandler<UpdateCustomFieldDefinitionCommand, CustomFieldDefinitionDto>
{
    public async Task<Result<CustomFieldDefinitionDto>> Handle(
        UpdateCustomFieldDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var definitionId = CustomFieldDefinitionId.From(request.DefinitionId);

        var definition = await dbContext.CustomFieldDefinitions.FirstOrDefaultAsync(
            d => d.Id == definitionId && d.TenantId == tenantId,
            cancellationToken);

        if (definition is null)
        {
            logger.LogWarning("Custom field definition {DefinitionId} not found for tenant {TenantId}",
                request.DefinitionId, tenantId);
            return Result<CustomFieldDefinitionDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_custom_field_definition_not_found"));
        }

        var duplicate = await dbContext.CustomFieldDefinitions.AnyAsync(
            d => d.TenantId == tenantId && d.FieldName == request.FieldName.Trim()
                 && d.IsActive && d.Id != definitionId,
            cancellationToken);

        if (duplicate)
        {
            logger.LogWarning("Custom field definition name {FieldName} already exists for tenant {TenantId}",
                request.FieldName, tenantId);
            return Result<CustomFieldDefinitionDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_custom_field_name_duplicate"));
        }

        definition.Update(request.FieldName, request.Options, request.IsRequired, request.DisplayOrder);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Custom field definition {DefinitionId} updated for tenant {TenantId}",
            definitionId, tenantId);

        var dto = new CustomFieldDefinitionDto(
            definition.Id.Value, definition.FieldName, definition.FieldType,
            definition.Options, definition.IsRequired, definition.DisplayOrder,
            definition.IsActive, definition.CreatedAt);

        return Result<CustomFieldDefinitionDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_custom_field_definition_updated"));
    }
}
