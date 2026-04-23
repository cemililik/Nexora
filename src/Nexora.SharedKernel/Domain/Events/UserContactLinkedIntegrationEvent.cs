namespace Nexora.SharedKernel.Domain.Events;

/// <summary>
/// Published by the Identity module when a user is linked to a Contacts module contact record.
/// Enables downstream modules (CRM, Donations, etc.) to associate user activity with the contact.
/// </summary>
public sealed record UserContactLinkedIntegrationEvent : IntegrationEventBase
{
    /// <summary>The Identity user that was linked.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The Contacts contact the user was linked to.</summary>
    public required Guid ContactId { get; init; }

    /// <summary>UTC timestamp when the link was created.</summary>
    public required DateTime LinkedAtUtc { get; init; }

    /// <summary>The user (admin) who performed the linking action.</summary>
    public required Guid LinkedByUserId { get; init; }
}
