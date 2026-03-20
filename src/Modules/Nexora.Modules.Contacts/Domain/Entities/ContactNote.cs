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

    /// <summary>Creates a new note on a contact.</summary>
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

    /// <summary>Updates the note content.</summary>
    public void Update(string content)
    {
        Content = content.Trim();
    }

    /// <summary>Pins this note for quick access.</summary>
    public void Pin() => IsPinned = true;
    /// <summary>Unpins this note.</summary>
    public void Unpin() => IsPinned = false;
}
