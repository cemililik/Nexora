using Nexora.Modules.Contacts.Domain.Events;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Contacts.Domain.Entities;

/// <summary>
/// Append-only activity entry for a contact's timeline (360-degree view).
/// Activities come from all modules (CRM, Donations, Education, etc.).
/// </summary>
public sealed class ContactActivity : Entity<ContactActivityId>
{
    public ContactId ContactId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string ModuleSource { get; private set; } = default!;
    public string ActivityType { get; private set; } = default!;
    public string Summary { get; private set; } = default!;
    public string? Details { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private ContactActivity() { }

    /// <summary>Creates a new activity entry for a contact's timeline.</summary>
    public static ContactActivity Create(
        ContactId contactId,
        Guid organizationId,
        string moduleSource,
        string activityType,
        string summary,
        string? details = null)
    {
        var activity = new ContactActivity
        {
            Id = ContactActivityId.New(),
            ContactId = contactId,
            OrganizationId = organizationId,
            ModuleSource = moduleSource,
            ActivityType = activityType,
            Summary = summary,
            Details = details,
            OccurredAt = DateTimeOffset.UtcNow
        };
        activity.AddDomainEvent(new ContactActivityLoggedEvent(contactId, activityType));
        return activity;
    }
}
