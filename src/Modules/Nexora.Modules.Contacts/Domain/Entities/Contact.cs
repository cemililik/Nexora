using Nexora.Modules.Contacts.Domain.Events;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Contacts.Domain.Entities;

/// <summary>
/// Aggregate root representing a contact (individual or organization).
/// Visible tenant-wide; tags and notes are org-scoped.
/// </summary>
public sealed class Contact : AuditableEntity<ContactId>, IAggregateRoot
{
    public Guid TenantId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public ContactType Type { get; private set; }
    public string? Title { get; private set; }
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string DisplayName { get; private set; } = default!;
    public string? CompanyName { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Mobile { get; private set; }
    public string? Website { get; private set; }
    public string? TaxId { get; private set; }
    public string Language { get; private set; } = "en";
    public string Currency { get; private set; } = "USD";
    public ContactSource Source { get; private set; }
    public ContactStatus Status { get; private set; }
    public ContactId? MergedIntoId { get; private set; }
    public string? Metadata { get; private set; }

    private readonly List<ContactAddress> _addresses = [];
    public IReadOnlyList<ContactAddress> Addresses => _addresses.AsReadOnly();

    private readonly List<ContactTag> _tags = [];
    public IReadOnlyList<ContactTag> Tags => _tags.AsReadOnly();

    private Contact() { }

    /// <summary>Creates a new contact with the specified details.</summary>
    public static Contact Create(
        Guid tenantId,
        Guid organizationId,
        ContactType type,
        string? firstName,
        string? lastName,
        string? companyName,
        string? email,
        string? phone,
        ContactSource source,
        string? title = null)
    {
        var name = ContactName.Create(firstName, lastName, companyName);
        var contact = new Contact
        {
            Id = ContactId.New(),
            TenantId = tenantId,
            OrganizationId = organizationId,
            Type = type,
            Title = title,
            FirstName = name.FirstName,
            LastName = name.LastName,
            DisplayName = name.DisplayName,
            CompanyName = name.CompanyName,
            Email = email?.ToLowerInvariant().Trim(),
            Phone = phone?.Trim(),
            Source = source,
            Status = ContactStatus.Active
        };
        contact.AddDomainEvent(new ContactCreatedEvent(contact.Id, type, contact.Email ?? string.Empty));
        return contact;
    }

    /// <summary>Updates the contact's profile information.</summary>
    public void Update(
        string? firstName,
        string? lastName,
        string? companyName,
        string? email,
        string? phone,
        string? mobile,
        string? website,
        string? taxId,
        string language,
        string currency,
        string? title = null)
    {
        var name = ContactName.Create(firstName, lastName, companyName);
        Title = title;
        FirstName = name.FirstName;
        LastName = name.LastName;
        DisplayName = name.DisplayName;
        CompanyName = name.CompanyName;
        Email = email?.ToLowerInvariant().Trim();
        Phone = phone?.Trim();
        Mobile = mobile?.Trim();
        Website = website?.Trim();
        TaxId = taxId?.Trim();
        Language = language;
        Currency = currency;
        AddDomainEvent(new ContactUpdatedEvent(Id));
    }

    /// <summary>Archives the contact, preventing further modifications.</summary>
    public void Archive()
    {
        if (Status is ContactStatus.Archived or ContactStatus.Merged)
            throw new DomainException("lockey_contacts_error_cannot_archive");

        Status = ContactStatus.Archived;
        AddDomainEvent(new ContactArchivedEvent(Id));
    }

    /// <summary>Restores an archived contact back to active status.</summary>
    public void Restore()
    {
        if (Status is not ContactStatus.Archived)
            throw new DomainException("lockey_contacts_error_only_archived_can_restore");

        Status = ContactStatus.Active;
        AddDomainEvent(new ContactRestoredEvent(Id));
    }

    /// <summary>Marks this contact as merged into the specified primary contact.</summary>
    public void MarkMerged(ContactId primaryContactId)
    {
        if (Status is ContactStatus.Merged)
            throw new DomainException("lockey_contacts_error_already_merged");

        Status = ContactStatus.Merged;
        MergedIntoId = primaryContactId;
        AddDomainEvent(new ContactMergedEvent(primaryContactId, Id));
    }
}
