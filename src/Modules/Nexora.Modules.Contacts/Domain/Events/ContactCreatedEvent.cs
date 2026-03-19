using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Domain.Events;

/// <summary>Raised when a new contact is created.</summary>
public sealed record ContactCreatedEvent(ContactId ContactId, ContactType Type, string Email) : DomainEventBase;
