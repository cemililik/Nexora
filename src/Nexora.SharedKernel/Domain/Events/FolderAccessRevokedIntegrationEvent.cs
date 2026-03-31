namespace Nexora.SharedKernel.Domain.Events;

/// <summary>Published when folder access is revoked.</summary>
public sealed record FolderAccessRevokedIntegrationEvent : IntegrationEventBase
{
    /// <summary>Gets the folder identifier.</summary>
    public required Guid FolderId { get; init; }

    /// <summary>Gets the folder access record identifier that was revoked.</summary>
    public required Guid AccessId { get; init; }
}
