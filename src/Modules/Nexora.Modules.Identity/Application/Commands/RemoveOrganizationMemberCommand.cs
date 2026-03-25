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

/// <summary>Command to remove a user from an organization.</summary>
public sealed record RemoveOrganizationMemberCommand(
    Guid OrganizationId,
    Guid UserId) : ICommand;

/// <summary>Validates organization member removal input.</summary>
public sealed class RemoveOrganizationMemberValidator : AbstractValidator<RemoveOrganizationMemberCommand>
{
    public RemoveOrganizationMemberValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("lockey_identity_validation_org_id_required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("lockey_identity_validation_user_id_required");
    }
}

/// <summary>Removes a user's membership from an organization.</summary>
public sealed class RemoveOrganizationMemberHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<RemoveOrganizationMemberHandler> logger) : ICommandHandler<RemoveOrganizationMemberCommand>
{
    public async Task<Result> Handle(
        RemoveOrganizationMemberCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var orgId = OrganizationId.From(request.OrganizationId);
        var userId = UserId.From(request.UserId);

        // Verify organization belongs to tenant
        var orgExists = await dbContext.Organizations
            .AnyAsync(o => o.Id == orgId && o.TenantId == tenantId, cancellationToken);

        if (!orgExists)
        {
            logger.LogWarning("Organization {OrganizationId} not found for tenant {TenantId}", request.OrganizationId, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_org_not_found"));
        }

        var membership = await dbContext.OrganizationUsers
            .FirstOrDefaultAsync(ou => ou.OrganizationId == orgId && ou.UserId == userId, cancellationToken);

        if (membership is null)
        {
            logger.LogWarning("User {UserId} is not a member of organization {OrganizationId}", request.UserId, request.OrganizationId);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_user_not_member"));
        }

        dbContext.OrganizationUsers.Remove(membership);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Member {UserId} removed from organization {OrganizationId}", request.UserId, request.OrganizationId);

        return Result.Success(LocalizedMessage.Of("lockey_identity_org_member_removed"));
    }
}
