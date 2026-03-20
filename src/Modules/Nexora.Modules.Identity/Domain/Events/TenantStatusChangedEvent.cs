using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Identity.Domain.Events;

/// <summary>Raised when a tenant's status changes (activate, suspend, terminate).</summary>
public sealed record TenantStatusChangedEvent(TenantId TenantId, string NewStatus) : DomainEventBase;
