using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Domain.Entities;

/// <summary>Represents a role assignment for a user within an organization.</summary>
public sealed class UserRole : Entity<UserRoleId>
{
    public OrganizationUserId OrganizationUserId { get; private set; }
    public RoleId RoleId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }

    private UserRole() { }

    /// <summary>Creates a new user-role assignment.</summary>
    public static UserRole Create(OrganizationUserId organizationUserId, RoleId roleId)
    {
        return new UserRole
        {
            Id = UserRoleId.New(),
            OrganizationUserId = organizationUserId,
            RoleId = roleId,
            AssignedAt = DateTimeOffset.UtcNow
        };
    }
}
