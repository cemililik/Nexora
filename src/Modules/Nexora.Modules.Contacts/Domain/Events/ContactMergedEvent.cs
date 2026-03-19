using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Domain.Events;

/// <summary>Raised when two contacts are merged.</summary>
public sealed record ContactMergedEvent(ContactId PrimaryContactId, ContactId SecondaryContactId) : DomainEventBase;
