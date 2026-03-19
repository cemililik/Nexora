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

/// <summary>Command to create a new tag.</summary>
public sealed record CreateTagCommand(
    string Name,
    string Category,
    string? Color = null) : ICommand<TagDto>;

/// <summary>Validates tag creation input.</summary>
public sealed class CreateTagValidator : AbstractValidator<CreateTagCommand>
{
    public CreateTagValidator()
    {
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

/// <summary>Creates a tag and persists it to the database.</summary>
public sealed class CreateTagHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<CreateTagHandler> logger) : ICommandHandler<CreateTagCommand, TagDto>
{
    public async Task<Result<TagDto>> Handle(
        CreateTagCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var category = Enum.Parse<TagCategory>(request.Category);

        var exists = await dbContext.Tags.AnyAsync(
            t => t.TenantId == tenantId && t.Name == request.Name.Trim(),
            cancellationToken);

        if (exists)
        {
            logger.LogWarning("Tag with name {TagName} already exists for tenant {TenantId}", request.Name, tenantId);
            return Result<TagDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_tag_name_duplicate"));
        }

        var tag = Tag.Create(tenantId, request.Name, category, request.Color);

        await dbContext.Tags.AddAsync(tag, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Tag {TagId} created for tenant {TenantId}", tag.Id, tenantId);

        return Result<TagDto>.Success(MapToDto(tag),
            LocalizedMessage.Of("lockey_contacts_tag_created"));
    }

    private static TagDto MapToDto(Tag t) => new(
        t.Id.Value, t.Name, t.Category.ToString(), t.Color, t.IsActive, t.CreatedAt);
}
