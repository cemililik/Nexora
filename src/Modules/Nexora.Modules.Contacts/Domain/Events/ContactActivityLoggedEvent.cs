using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Domain.Events;

/// <summary>Raised when an activity is logged on a contact.</summary>
public sealed record ContactActivityLoggedEvent(ContactId ContactId, string ActivityType) : DomainEventBase;
