using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Domain.Events;

/// <summary>Raised when a tag is removed from a contact.</summary>
public sealed record ContactTagRemovedEvent(ContactId ContactId, TagId TagId) : DomainEventBase;
