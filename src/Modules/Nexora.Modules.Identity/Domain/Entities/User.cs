using Nexora.Modules.Identity.Domain.Events;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Domain.Entities;

/// <summary>Represents a user within a tenant.</summary>
public sealed class User : AuditableEntity<UserId>, IAggregateRoot
{
    public TenantId TenantId { get; private set; }
    public string KeycloakUserId { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string? Phone { get; private set; }
    public string? AvatarUrl { get; private set; }
    public UserStatus Status { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    private readonly List<OrganizationUser> _organizationUsers = [];
    public IReadOnlyList<OrganizationUser> OrganizationUsers => _organizationUsers.AsReadOnly();

    public string FullName => $"{FirstName} {LastName}";

    private User() { }

    /// <summary>Creates a new user linked to a Keycloak identity.</summary>
    public static User Create(
        TenantId tenantId,
        string keycloakUserId,
        string email,
        string firstName,
        string lastName)
    {
        var user = new User
        {
            Id = UserId.New(),
            TenantId = tenantId,
            KeycloakUserId = keycloakUserId,
            Email = email.ToLowerInvariant(),
            FirstName = firstName,
            LastName = lastName,
            Status = UserStatus.Active
        };
        user.AddDomainEvent(new UserCreatedEvent(user.Id, tenantId, email));
        return user;
    }

    /// <summary>Updates the user's profile information.</summary>
    public void UpdateProfile(string firstName, string lastName, string? phone)
    {
        FirstName = firstName;
        LastName = lastName;
        Phone = phone;
    }

    /// <summary>Records the current timestamp as the user's last login.</summary>
    public void RecordLogin() => LastLoginAt = DateTimeOffset.UtcNow;
    /// <summary>Deactivates the user account.</summary>
    public void Deactivate() { Status = UserStatus.Inactive; AddDomainEvent(new UserDeactivatedEvent(Id)); }
    /// <summary>Activates the user account.</summary>
    public void Activate() => Status = UserStatus.Active;
}

/// <summary>Represents the status of a user account.</summary>
public enum UserStatus
{
    Active,
    Inactive,
    Locked
}
