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

/// <summary>Command to add a user as a member of an organization.</summary>
public sealed record AddOrganizationMemberCommand(
    Guid OrganizationId,
    Guid UserId,
    bool IsDefault = false) : ICommand<OrganizationMemberDto>;

/// <summary>Validates organization member addition input.</summary>
public sealed class AddOrganizationMemberValidator : AbstractValidator<AddOrganizationMemberCommand>
{
    public AddOrganizationMemberValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("lockey_identity_validation_org_id_required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("lockey_identity_validation_user_id_required");
    }
}

/// <summary>Adds a user to an organization after verifying both exist and membership is not duplicate.</summary>
public sealed class AddOrganizationMemberHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<AddOrganizationMemberHandler> logger) : ICommandHandler<AddOrganizationMemberCommand, OrganizationMemberDto>
{
    public async Task<Result<OrganizationMemberDto>> Handle(
        AddOrganizationMemberCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var orgId = OrganizationId.From(request.OrganizationId);
        var userId = UserId.From(request.UserId);

        // Verify organization exists and belongs to tenant
        var orgExists = await dbContext.Organizations
            .AnyAsync(o => o.Id == orgId && o.TenantId == tenantId, cancellationToken);

        if (!orgExists)
        {
            logger.LogWarning("Organization {OrganizationId} not found for tenant {TenantId}", request.OrganizationId, tenantId);
            return Result<OrganizationMemberDto>.Failure(LocalizedMessage.Of("lockey_identity_error_org_not_found"));
        }

        // Verify user exists in tenant
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("User {UserId} not found for tenant {TenantId}", request.UserId, tenantId);
            return Result<OrganizationMemberDto>.Failure(LocalizedMessage.Of("lockey_identity_error_user_not_found"));
        }

        // Check duplicate membership
        var alreadyMember = await dbContext.OrganizationUsers
            .AnyAsync(ou => ou.OrganizationId == orgId && ou.UserId == userId, cancellationToken);

        if (alreadyMember)
        {
            logger.LogWarning("User {UserId} is already a member of organization {OrganizationId}", request.UserId, request.OrganizationId);
            return Result<OrganizationMemberDto>.Failure(LocalizedMessage.Of("lockey_identity_error_user_already_member"));
        }

        var orgUser = OrganizationUser.Create(userId, orgId, request.IsDefault);

        await dbContext.OrganizationUsers.AddAsync(orgUser, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = new OrganizationMemberDto(
            user.Id.Value, user.Email, user.FirstName, user.LastName, orgUser.IsDefaultOrg);

        logger.LogInformation("Member {UserId} added to organization {OrganizationId}", request.UserId, request.OrganizationId);

        return Result<OrganizationMemberDto>.Success(dto,
            LocalizedMessage.Of("lockey_identity_org_member_added"));
    }
}
