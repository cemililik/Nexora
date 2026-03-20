using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Domain.Entities;

/// <summary>Represents a user's membership in an organization.</summary>
public sealed class OrganizationUser : Entity<OrganizationUserId>
{
    public UserId UserId { get; private set; }
    public OrganizationId OrganizationId { get; private set; }
    public bool IsDefaultOrg { get; private set; }

    private readonly List<UserRole> _userRoles = [];
    public IReadOnlyList<UserRole> UserRoles => _userRoles.AsReadOnly();

    private OrganizationUser() { }

    /// <summary>Creates a new organization-user membership.</summary>
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

    /// <summary>Marks this organization as the user's default.</summary>
    public void SetAsDefault() => IsDefaultOrg = true;
    /// <summary>Removes the default organization designation.</summary>
    public void UnsetDefault() => IsDefaultOrg = false;
}
