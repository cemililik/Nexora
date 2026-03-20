using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Documents.Domain.Events;

/// <summary>Raised when access is granted on a document.</summary>
public sealed record DocumentAccessGrantedEvent(
    DocumentId DocumentId, Guid? UserId, Guid? RoleId, AccessPermission Permission) : DomainEventBase;
