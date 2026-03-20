using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Identity.Domain.Events;

/// <summary>Raised when a new user is created.</summary>
public sealed record UserCreatedEvent(UserId UserId, TenantId TenantId, string Email) : DomainEventBase;
