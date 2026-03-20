using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to create a new organization within the current tenant.</summary>
public sealed record CreateOrganizationCommand(
    string Name,
    string Slug) : ICommand<OrganizationDto>;

/// <summary>Validates organization creation input.</summary>
public sealed class CreateOrganizationValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("lockey_identity_validation_org_name_required")
            .MaximumLength(200).WithMessage("lockey_identity_validation_org_name_max_length");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("lockey_identity_validation_org_slug_required")
            .MaximumLength(100).WithMessage("lockey_identity_validation_org_slug_max_length")
            .Matches("^[a-z0-9-]+$").WithMessage("lockey_identity_validation_org_slug_format");
    }
}

/// <summary>Creates an organization and persists it to the database.</summary>
public sealed class CreateOrganizationHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<CreateOrganizationHandler> logger) : ICommandHandler<CreateOrganizationCommand, OrganizationDto>
{
    public async Task<Result<OrganizationDto>> Handle(
        CreateOrganizationCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);

        var slugExists = await dbContext.Organizations
            .AnyAsync(o => o.TenantId == tenantId && o.Slug == request.Slug, cancellationToken);

        if (slugExists)
        {
            logger.LogWarning("Organization creation failed: slug {Slug} already taken for tenant {TenantId}", request.Slug, tenantId);
            return Result<OrganizationDto>.Failure(
                "lockey_identity_error_org_slug_taken",
                new Dictionary<string, string> { ["slug"] = request.Slug });
        }

        var organization = Organization.Create(tenantId, request.Name, request.Slug);
        await dbContext.Organizations.AddAsync(organization, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = new OrganizationDto(
            organization.Id.Value,
            organization.Name,
            organization.Slug,
            organization.LogoUrl,
            organization.Timezone,
            organization.DefaultCurrency,
            organization.DefaultLanguage,
            organization.IsActive);

        logger.LogInformation("Organization {OrganizationId} created with slug {Slug} for tenant {TenantId}", organization.Id, organization.Slug, tenantId);

        return Result<OrganizationDto>.Success(dto,
            new LocalizedMessage("lockey_identity_org_created"));
    }
}
