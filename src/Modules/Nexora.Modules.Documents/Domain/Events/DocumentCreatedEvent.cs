using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Documents.Domain.Events;

/// <summary>Raised when a document is uploaded.</summary>
public sealed record DocumentCreatedEvent(DocumentId DocumentId, string Name, string MimeType, long FileSize) : DomainEventBase;
