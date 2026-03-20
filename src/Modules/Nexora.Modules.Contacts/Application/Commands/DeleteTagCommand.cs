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

/// <summary>Command to deactivate (soft-delete) a tag.</summary>
public sealed record DeleteTagCommand(Guid TagId) : ICommand;

/// <summary>Deactivates a tag so it can no longer be assigned.</summary>
public sealed class DeleteTagHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<DeleteTagHandler> logger) : ICommandHandler<DeleteTagCommand>
{
    public async Task<Result> Handle(
        DeleteTagCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var tagId = TagId.From(request.TagId);

        var tag = await dbContext.Tags.FirstOrDefaultAsync(
            t => t.Id == tagId && t.TenantId == tenantId,
            cancellationToken);

        if (tag is null)
        {
            logger.LogWarning("Tag {TagId} not found for tenant {TenantId}", request.TagId, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_tag_not_found"));
        }

        if (!tag.IsActive)
        {
            logger.LogWarning("Tag {TagId} is already deactivated", request.TagId);
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_tag_already_deactivated"));
        }

        tag.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Tag {TagId} deactivated for tenant {TenantId}", tag.Id, tenantId);

        return Result.Success(
            LocalizedMessage.Of("lockey_contacts_tag_deactivated"));
    }
}

/// <summary>Validates delete tag input.</summary>
public sealed class DeleteTagCommandValidator : AbstractValidator<DeleteTagCommand>
{
    public DeleteTagCommandValidator()
    {
        RuleFor(x => x.TagId).NotEmpty().WithMessage("lockey_validation_required");
    }
}
