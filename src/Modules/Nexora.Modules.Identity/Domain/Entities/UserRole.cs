using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Domain.Entities;

public sealed class UserRole : Entity<UserRoleId>
{
    public OrganizationUserId OrganizationUserId { get; private set; }
    public RoleId RoleId { get; private set; }

    private UserRole() { }

    public static UserRole Create(OrganizationUserId organizationUserId, RoleId roleId)
    {
        return new UserRole
        {
            Id = UserRoleId.New(),
            OrganizationUserId = organizationUserId,
            RoleId = roleId
        };
    }
}
