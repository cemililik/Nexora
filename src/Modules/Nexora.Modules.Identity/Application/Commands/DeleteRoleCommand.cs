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

public sealed record DeleteRoleCommand(Guid Id) : ICommand;

public sealed class DeleteRoleValidator : AbstractValidator<DeleteRoleCommand>
{
    public DeleteRoleValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("lockey_validation_required");
    }
}

public sealed class DeleteRoleHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<DeleteRoleHandler> logger) : ICommandHandler<DeleteRoleCommand>
{
    public async Task<Result> Handle(DeleteRoleCommand request, CancellationToken ct)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var roleId = RoleId.From(request.Id);

        var role = await dbContext.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, ct);

        if (role is null)
        {
            logger.LogWarning("Role {RoleId} not found for tenant {TenantId}", request.Id, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_role_not_found"));
        }

        if (role.IsSystemRole)
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_system_role_immutable"));

        // Check if role is assigned to any users
        var assignedCount = await dbContext.UserRoles.CountAsync(ur => ur.RoleId == roleId, ct);
        if (assignedCount > 0)
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_role_has_users",
                new() { ["count"] = assignedCount.ToString() }));

        dbContext.Roles.Remove(role);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Role {RoleId} deleted for tenant {TenantId}", role.Id, tenantId);

        return Result.Success(LocalizedMessage.Of("lockey_identity_toast_role_deleted"));
    }
}
