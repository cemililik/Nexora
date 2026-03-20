using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to soft-delete (deactivate) an organization.</summary>
public sealed record DeleteOrganizationCommand(Guid OrganizationId) : ICommand;

/// <summary>Validates organization delete input.</summary>
public sealed class DeleteOrganizationValidator : AbstractValidator<DeleteOrganizationCommand>
{
    public DeleteOrganizationValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("lockey_identity_validation_org_id_required");
    }
}

/// <summary>Soft-deletes an organization by deactivating it.</summary>
public sealed class DeleteOrganizationHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<DeleteOrganizationHandler> logger) : ICommandHandler<DeleteOrganizationCommand>
{
    public async Task<Result> Handle(
        DeleteOrganizationCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var orgId = OrganizationId.From(request.OrganizationId);

        var org = await dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == tenantId, cancellationToken);

        if (org is null)
        {
            logger.LogWarning("Organization deletion failed: organization {OrganizationId} not found for tenant {TenantId}", request.OrganizationId, tenantId);
            return Result.Failure("lockey_identity_error_org_not_found");
        }

        org.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Organization {OrganizationId} deactivated for tenant {TenantId}", org.Id, tenantId);

        return Result.Success(new LocalizedMessage("lockey_identity_org_deactivated"));
    }
}
