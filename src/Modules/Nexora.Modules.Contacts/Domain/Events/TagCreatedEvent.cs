using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Domain.Events;

/// <summary>Raised when a new tag is created.</summary>
public sealed record TagCreatedEvent(TagId TagId, string Name) : DomainEventBase;
