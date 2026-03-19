using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to update an organization's settings.</summary>
public sealed record UpdateOrganizationCommand(
    Guid OrganizationId,
    string Name,
    string Timezone,
    string DefaultCurrency,
    string DefaultLanguage) : ICommand<OrganizationDto>;

/// <summary>Validates organization update input.</summary>
public sealed class UpdateOrganizationValidator : AbstractValidator<UpdateOrganizationCommand>
{
    public UpdateOrganizationValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("lockey_identity_validation_org_id_required");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("lockey_identity_validation_org_name_required")
            .MaximumLength(200).WithMessage("lockey_identity_validation_org_name_max_length");

        RuleFor(x => x.Timezone)
            .NotEmpty().WithMessage("lockey_identity_validation_org_timezone_required")
            .MaximumLength(50).WithMessage("lockey_identity_validation_org_timezone_max_length");

        RuleFor(x => x.DefaultCurrency)
            .NotEmpty().WithMessage("lockey_identity_validation_org_currency_required")
            .Length(3).WithMessage("lockey_identity_validation_org_currency_length");

        RuleFor(x => x.DefaultLanguage)
            .NotEmpty().WithMessage("lockey_identity_validation_org_language_required")
            .MaximumLength(10).WithMessage("lockey_identity_validation_org_language_max_length");
    }
}

/// <summary>Updates an organization's configurable properties.</summary>
public sealed class UpdateOrganizationHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<UpdateOrganizationHandler> logger) : ICommandHandler<UpdateOrganizationCommand, OrganizationDto>
{
    public async Task<Result<OrganizationDto>> Handle(
        UpdateOrganizationCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var orgId = OrganizationId.From(request.OrganizationId);

        var org = await dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == tenantId, cancellationToken);

        if (org is null)
        {
            logger.LogWarning("Organization update failed: organization {OrganizationId} not found for tenant {TenantId}", request.OrganizationId, tenantId);
            return Result<OrganizationDto>.Failure("lockey_identity_error_org_not_found");
        }

        org.Update(request.Name, request.Timezone, request.DefaultCurrency, request.DefaultLanguage);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = new OrganizationDto(
            org.Id.Value, org.Name, org.Slug, org.LogoUrl,
            org.Timezone, org.DefaultCurrency, org.DefaultLanguage, org.IsActive);

        logger.LogInformation("Organization {OrganizationId} updated for tenant {TenantId}", org.Id, tenantId);

        return Result<OrganizationDto>.Success(dto,
            new LocalizedMessage("lockey_identity_org_updated"));
    }
}
