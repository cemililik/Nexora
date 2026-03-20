using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Domain.Events;

/// <summary>Raised when a contact is restored from archived status.</summary>
public sealed record ContactRestoredEvent(ContactId ContactId) : DomainEventBase;
