using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Domain.Events;

/// <summary>Raised when a contact's consent status changes.</summary>
public sealed record ConsentChangedEvent(ContactId ContactId, ConsentType ConsentType, bool Granted) : DomainEventBase;
