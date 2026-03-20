using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Identity.Domain.Events;

/// <summary>Raised when a user is deactivated.</summary>
public sealed record UserDeactivatedEvent(UserId UserId) : DomainEventBase;
