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

/// <summary>Command to update an existing tag.</summary>
public sealed record UpdateTagCommand(
    Guid TagId,
    string Name,
    string Category,
    string? Color) : ICommand<TagDto>;

/// <summary>Validates tag update input.</summary>
public sealed class UpdateTagValidator : AbstractValidator<UpdateTagCommand>
{
    public UpdateTagValidator()
    {
        RuleFor(x => x.TagId)
            .NotEmpty().WithMessage("lockey_contacts_validation_tag_id_required");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("lockey_contacts_validation_tag_name_required")
            .MaximumLength(100).WithMessage("lockey_contacts_validation_tag_name_max_length");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("lockey_contacts_validation_tag_category_required")
            .Must(c => Enum.TryParse<TagCategory>(c, out _))
            .WithMessage("lockey_contacts_validation_tag_category_invalid");

        RuleFor(x => x.Color)
            .MaximumLength(20).WithMessage("lockey_contacts_validation_tag_color_max_length");
    }
}

/// <summary>Updates a tag and persists changes.</summary>
public sealed class UpdateTagHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<UpdateTagHandler> logger) : ICommandHandler<UpdateTagCommand, TagDto>
{
    public async Task<Result<TagDto>> Handle(
        UpdateTagCommand request,
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
            return Result<TagDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_tag_not_found"));
        }

        var duplicateName = await dbContext.Tags.AnyAsync(
            t => t.TenantId == tenantId && t.Name == request.Name.Trim() && t.Id != tagId,
            cancellationToken);

        if (duplicateName)
        {
            logger.LogWarning("Tag name {TagName} already exists for tenant {TenantId}", request.Name, tenantId);
            return Result<TagDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_tag_name_duplicate"));
        }

        var category = Enum.Parse<TagCategory>(request.Category);
        tag.Update(request.Name, category, request.Color);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Tag {TagId} updated for tenant {TenantId}", tag.Id, tenantId);

        return Result<TagDto>.Success(MapToDto(tag),
            LocalizedMessage.Of("lockey_contacts_tag_updated"));
    }

    private static TagDto MapToDto(Tag t) => new(
        t.Id.Value, t.Name, t.Category.ToString(), t.Color, t.IsActive, t.CreatedAt);
}
