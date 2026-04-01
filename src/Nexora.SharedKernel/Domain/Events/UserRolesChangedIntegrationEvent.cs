namespace Nexora.SharedKernel.Domain.Events;

/// <summary>Published when a user's role assignments change, triggering permission cache invalidation.</summary>
public sealed record UserRolesChangedIntegrationEvent : IntegrationEventBase
{
    /// <summary>Gets the internal user identifier.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Gets the Keycloak subject identifier used as the permission cache key.</summary>
    public required string KeycloakUserId { get; init; }

    /// <summary>Gets the type of change: "Assigned", "Removed", or "Reconciled".</summary>
    public required string ChangeType { get; init; }
}
