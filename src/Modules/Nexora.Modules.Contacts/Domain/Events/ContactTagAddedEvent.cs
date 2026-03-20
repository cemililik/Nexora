using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Domain.Events;

/// <summary>Raised when a tag is added to a contact.</summary>
public sealed record ContactTagAddedEvent(ContactId ContactId, TagId TagId) : DomainEventBase;
