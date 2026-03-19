using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Domain.Entities;

public sealed class OrganizationUser : Entity<OrganizationUserId>
{
    public UserId UserId { get; private set; }
    public OrganizationId OrganizationId { get; private set; }
    public bool IsDefaultOrg { get; private set; }

    private readonly List<UserRole> _userRoles = [];
    public IReadOnlyList<UserRole> UserRoles => _userRoles.AsReadOnly();

    private OrganizationUser() { }

    public static OrganizationUser Create(UserId userId, OrganizationId organizationId, bool isDefault = false)
    {
        return new OrganizationUser
        {
            Id = OrganizationUserId.New(),
            UserId = userId,
            OrganizationId = organizationId,
            IsDefaultOrg = isDefault
        };
    }

    public void SetAsDefault() => IsDefaultOrg = true;
    public void UnsetDefault() => IsDefaultOrg = false;
}
