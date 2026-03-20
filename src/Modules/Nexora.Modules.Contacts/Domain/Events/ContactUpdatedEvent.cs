using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Domain.Events;

/// <summary>Raised when a contact is updated.</summary>
public sealed record ContactUpdatedEvent(ContactId ContactId) : DomainEventBase;
