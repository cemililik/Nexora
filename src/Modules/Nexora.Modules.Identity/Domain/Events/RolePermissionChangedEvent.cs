using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Identity.Domain.Events;

/// <summary>The kind of permission change performed on a role.</summary>
public enum PermissionAction
{
    Assigned,
    Revoked
}

/// <summary>Raised when role permissions are changed.</summary>
public sealed record RolePermissionChangedEvent(RoleId RoleId, PermissionAction Action) : DomainEventBase;
