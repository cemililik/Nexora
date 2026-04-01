using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Documents.Domain.Events;

/// <summary>Raised when access is revoked on a folder.</summary>
public sealed record FolderAccessRevokedEvent(
    FolderId FolderId,
    FolderAccessId AccessId) : DomainEventBase;
