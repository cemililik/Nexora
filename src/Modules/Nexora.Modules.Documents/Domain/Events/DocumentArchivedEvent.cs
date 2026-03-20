using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Documents.Domain.Events;

/// <summary>Raised when a document is archived.</summary>
public sealed record DocumentArchivedEvent(DocumentId DocumentId) : DomainEventBase;
