using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Documents.Domain.Events;

/// <summary>Raised when a new version is added to a document.</summary>
public sealed record DocumentVersionAddedEvent(DocumentId DocumentId, int VersionNumber) : DomainEventBase;
