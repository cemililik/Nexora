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

/// <summary>Command to deactivate a custom field definition.</summary>
public sealed record DeleteCustomFieldDefinitionCommand(Guid DefinitionId) : ICommand;

/// <summary>Deactivates a custom field definition.</summary>
public sealed class DeleteCustomFieldDefinitionHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<DeleteCustomFieldDefinitionHandler> logger) : ICommandHandler<DeleteCustomFieldDefinitionCommand>
{
    public async Task<Result> Handle(
        DeleteCustomFieldDefinitionCommand request,
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
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_custom_field_definition_not_found"));
        }

        if (!definition.IsActive)
        {
            logger.LogWarning("Custom field definition {DefinitionId} already deactivated", request.DefinitionId);
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_custom_field_definition_already_deactivated"));
        }

        definition.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Custom field definition {DefinitionId} deactivated for tenant {TenantId}",
            definitionId, tenantId);

        return Result.Success(LocalizedMessage.Of("lockey_contacts_custom_field_definition_deleted"));
    }
}

/// <summary>Validates delete custom field definition input.</summary>
public sealed class DeleteCustomFieldDefinitionCommandValidator : AbstractValidator<DeleteCustomFieldDefinitionCommand>
{
    public DeleteCustomFieldDefinitionCommandValidator()
    {
        RuleFor(x => x.DefinitionId).NotEmpty().WithMessage("lockey_validation_required");
    }
}
