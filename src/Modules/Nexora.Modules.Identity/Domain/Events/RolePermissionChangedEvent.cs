using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Identity.Domain.Events;

/// <summary>Raised when role permissions are changed.</summary>
public sealed record RolePermissionChangedEvent(RoleId RoleId, string Action) : DomainEventBase;
