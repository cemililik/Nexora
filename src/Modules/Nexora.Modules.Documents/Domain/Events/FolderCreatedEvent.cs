using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Documents.Domain.Events;

/// <summary>Raised when a new folder is created.</summary>
public sealed record FolderCreatedEvent(FolderId FolderId, string Name) : DomainEventBase;
