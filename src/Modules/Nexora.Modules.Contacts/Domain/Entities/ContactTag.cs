using Nexora.Modules.Contacts.Domain.Events;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Contacts.Domain.Entities;

/// <summary>
/// Association between a contact and a tag, scoped to an organization.
/// Each organization independently assigns tags to contacts.
/// </summary>
public sealed class ContactTag : Entity<ContactTagId>
{
    public ContactId ContactId { get; private set; }
    public TagId TagId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }

    private ContactTag() { }

    public static ContactTag Create(ContactId contactId, TagId tagId, Guid organizationId)
    {
        var tag = new ContactTag
        {
            Id = ContactTagId.New(),
            ContactId = contactId,
            TagId = tagId,
            OrganizationId = organizationId,
            AssignedAt = DateTimeOffset.UtcNow
        };

        tag.AddDomainEvent(new ContactTagAddedEvent(tag.ContactId, tag.TagId));

        return tag;
    }
}
