namespace Nexora.SharedKernel.Domain.Events;

/// <summary>Published when folder access is granted to a user or role.</summary>
public sealed record FolderAccessGrantedIntegrationEvent : IntegrationEventBase
{
    /// <summary>Gets the folder identifier.</summary>
    public required Guid FolderId { get; init; }

    /// <summary>Gets the folder access record identifier.</summary>
    public required Guid AccessId { get; init; }

    /// <summary>Gets the granted user identifier, or null if role-based.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Gets the granted role identifier, or null if user-based.</summary>
    public Guid? RoleId { get; init; }

    /// <summary>Gets the permission level granted.</summary>
    public required string Permission { get; init; }
}
