using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Domain.Events;

/// <summary>Raised when a contact is archived (soft-deleted).</summary>
public sealed record ContactArchivedEvent(ContactId ContactId) : DomainEventBase;
