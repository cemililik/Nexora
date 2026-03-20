using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>Command to create a custom field definition.</summary>
public sealed record CreateCustomFieldDefinitionCommand(
    string FieldName,
    string FieldType,
    string? Options = null,
    bool IsRequired = false,
    int DisplayOrder = 0) : ICommand<CustomFieldDefinitionDto>;

/// <summary>Validates custom field definition creation input.</summary>
public sealed class CreateCustomFieldDefinitionValidator : AbstractValidator<CreateCustomFieldDefinitionCommand>
{
    private static readonly string[] ValidFieldTypes = ["text", "number", "date", "boolean", "select", "multiselect"];

    public CreateCustomFieldDefinitionValidator()
    {
        RuleFor(x => x.FieldName)
            .NotEmpty().WithMessage("lockey_contacts_validation_field_name_required")
            .MaximumLength(100).WithMessage("lockey_contacts_validation_field_name_max_length");

        RuleFor(x => x.FieldType)
            .NotEmpty().WithMessage("lockey_contacts_validation_field_type_required")
            .Must(t => ValidFieldTypes.Contains(t.ToLowerInvariant()))
            .WithMessage("lockey_contacts_validation_field_type_invalid");

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("lockey_contacts_validation_display_order_negative");
    }
}

/// <summary>Creates a custom field definition for the tenant.</summary>
public sealed class CreateCustomFieldDefinitionHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<CreateCustomFieldDefinitionHandler> logger) : ICommandHandler<CreateCustomFieldDefinitionCommand, CustomFieldDefinitionDto>
{
    public async Task<Result<CustomFieldDefinitionDto>> Handle(
        CreateCustomFieldDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);

        var duplicate = await dbContext.CustomFieldDefinitions.AnyAsync(
            d => d.TenantId == tenantId && d.FieldName == request.FieldName.Trim() && d.IsActive,
            cancellationToken);

        if (duplicate)
        {
            logger.LogWarning("Custom field definition {FieldName} already exists for tenant {TenantId}",
                request.FieldName, tenantId);
            return Result<CustomFieldDefinitionDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_custom_field_name_duplicate"));
        }

        var definition = CustomFieldDefinition.Create(
            tenantId, request.FieldName, request.FieldType,
            request.Options, request.IsRequired, request.DisplayOrder);

        await dbContext.CustomFieldDefinitions.AddAsync(definition, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Custom field definition {FieldName} created for tenant {TenantId}",
            definition.FieldName, tenantId);

        var dto = new CustomFieldDefinitionDto(
            definition.Id.Value, definition.FieldName, definition.FieldType,
            definition.Options, definition.IsRequired, definition.DisplayOrder,
            definition.IsActive, definition.CreatedAt);

        return Result<CustomFieldDefinitionDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_custom_field_definition_created"));
    }
}
