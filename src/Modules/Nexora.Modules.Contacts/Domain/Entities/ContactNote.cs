using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Contacts.Domain.Entities;

/// <summary>
/// Organization-scoped note on a contact. Notes can be pinned for quick access.
/// </summary>
public sealed class ContactNote : AuditableEntity<ContactNoteId>
{
    public ContactId ContactId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Content { get; private set; } = default!;
    public bool IsPinned { get; private set; }

    private ContactNote() { }

    public static ContactNote Create(
        ContactId contactId,
        Guid authorUserId,
        Guid organizationId,
        string content)
    {
        return new ContactNote
        {
            Id = ContactNoteId.New(),
            ContactId = contactId,
            AuthorUserId = authorUserId,
            OrganizationId = organizationId,
            Content = content.Trim(),
            IsPinned = false
        };
    }

    public void Update(string content)
    {
        Content = content.Trim();
    }

    public void Pin() => IsPinned = true;
    public void Unpin() => IsPinned = false;
}
