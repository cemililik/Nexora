using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Documents.Domain.Events;

/// <summary>Raised when access is granted on a folder to a user or role.</summary>
public sealed record FolderAccessGrantedEvent(
    FolderId FolderId,
    FolderAccessId AccessId,
    Guid? UserId,
    Guid? RoleId,
    AccessPermission Permission) : DomainEventBase;
