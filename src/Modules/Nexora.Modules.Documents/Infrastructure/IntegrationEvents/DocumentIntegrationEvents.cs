using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Documents.Infrastructure.IntegrationEvents;

/// <summary>Published when a document is uploaded.</summary>
public sealed record DocumentUploadedIntegrationEvent : IntegrationEventBase
{
    public required Guid DocumentId { get; init; }
    public required string Name { get; init; }
    public required string MimeType { get; init; }
    public required long FileSize { get; init; }
    public required Guid FolderId { get; init; }
    public required Guid? LinkedEntityId { get; init; }
    public required string? LinkedEntityType { get; init; }
}

/// <summary>Published when a document is archived.</summary>
public sealed record DocumentArchivedIntegrationEvent : IntegrationEventBase
{
    public required Guid DocumentId { get; init; }
}

/// <summary>Published when a recipient signs a document.</summary>
public sealed record DocumentSignedIntegrationEvent : IntegrationEventBase
{
    public required Guid SignatureRequestId { get; init; }
    public required Guid DocumentId { get; init; }
    public required Guid RecipientContactId { get; init; }
}

/// <summary>Published when all recipients have signed a signature request.</summary>
public sealed record SignatureCompletedIntegrationEvent : IntegrationEventBase
{
    public required Guid SignatureRequestId { get; init; }
    public required Guid DocumentId { get; init; }
}
