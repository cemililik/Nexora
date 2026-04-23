using Nexora.Modules.Identity.Domain.Events;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

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

    /// <summary>BCP 47 language tag preferred by this user for UI display (e.g. "en", "tr"). Null means use tenant default.</summary>
    public string? PreferredLanguage { get; private set; }

    /// <summary>
    /// Optional link to a Contacts module Contact record for 360° view (e.g. a staff member who is also a donor).
    /// Raw <see cref="Guid"/> — the Contacts <c>ContactId</c> value object stays inside the Contacts module boundary.
    /// </summary>
    public Guid? ContactId { get; private set; }

    /// <summary>
    /// True when this user represents a Keycloak service account (API client / bot) — never a human.
    /// Detected via the Keycloak naming convention: service-account IDs are prefixed with "service-account-".
    /// System accounts MUST NOT be linked to contact records.
    /// </summary>
    public bool IsSystemAccount => KeycloakUserId?.StartsWith("service-account-") == true;

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

    /// <summary>
    /// Updates the user's locale preference. Pass <see langword="null"/> to clear (falls back to tenant default).
    /// </summary>
    public void UpdatePreferences(string? preferredLanguage)
    {
        PreferredLanguage = string.IsNullOrWhiteSpace(preferredLanguage)
            ? null
            : preferredLanguage.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Links this user to a Contacts module contact record.
    /// </summary>
    /// <param name="contactId">The Contacts <c>ContactId</c> to link.</param>
    /// <param name="linkedByUserId">The user performing the link (for audit/event trail).</param>
    /// <exception cref="DomainException">
    /// Thrown when the user is a system account or already linked to a contact.
    /// </exception>
    public void LinkContact(Guid contactId, UserId linkedByUserId)
    {
        if (contactId == Guid.Empty)
            throw new DomainException("lockey_identity_user_link_contact_contact_id_required");

        if (IsSystemAccount)
            throw new DomainException("lockey_identity_user_link_contact_system_account_rejected");

        if (ContactId is not null)
            throw new DomainException("lockey_identity_user_link_contact_already_linked");

        ContactId = contactId;
        AddDomainEvent(new UserContactLinkedDomainEvent(Id, TenantId, contactId, linkedByUserId));
    }

    /// <summary>
    /// Removes the link between this user and its contact record. Idempotent — a no-op when not linked.
    /// </summary>
    /// <param name="unlinkedByUserId">The actor user who performed the unlink (for audit/event trail).</param>
    public void UnlinkContact(UserId unlinkedByUserId)
    {
        if (ContactId is null)
            return;

        var previous = ContactId.Value;
        ContactId = null;
        AddDomainEvent(new UserContactUnlinkedDomainEvent(Id, TenantId, previous, unlinkedByUserId));
    }
}

/// <summary>Represents the status of a user account.</summary>
public enum UserStatus
{
    Active,
    Inactive,
    Locked
}
