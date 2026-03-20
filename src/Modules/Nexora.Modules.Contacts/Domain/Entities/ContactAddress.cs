using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Contacts.Domain.Entities;

/// <summary>Physical or mailing address for a contact.</summary>
public sealed class ContactAddress : AuditableEntity<ContactAddressId>
{
    public ContactId ContactId { get; private set; }
    public AddressType Type { get; private set; }
    public string Street1 { get; private set; } = default!;
    public string? Street2 { get; private set; }
    public string City { get; private set; } = default!;
    public string? State { get; private set; }
    public string? PostalCode { get; private set; }
    public string CountryCode { get; private set; } = default!;
    public bool IsPrimary { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    private ContactAddress() { }

    /// <summary>Creates a new address for a contact.</summary>
    public static ContactAddress Create(
        ContactId contactId,
        AddressType type,
        string street1,
        string city,
        string countryCode,
        string? street2 = null,
        string? state = null,
        string? postalCode = null,
        bool isPrimary = false)
    {
        return new ContactAddress
        {
            Id = ContactAddressId.New(),
            ContactId = contactId,
            Type = type,
            Street1 = street1,
            Street2 = street2,
            City = city,
            State = state,
            PostalCode = postalCode,
            CountryCode = countryCode.ToUpperInvariant(),
            IsPrimary = isPrimary
        };
    }

    /// <summary>Updates the address details.</summary>
    public void Update(
        AddressType type,
        string street1,
        string city,
        string countryCode,
        string? street2 = null,
        string? state = null,
        string? postalCode = null)
    {
        Type = type;
        Street1 = street1;
        Street2 = street2;
        City = city;
        State = state;
        PostalCode = postalCode;
        CountryCode = countryCode.ToUpperInvariant();
    }

    /// <summary>Sets whether this address is the primary address.</summary>
    public void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;

    /// <summary>Sets the geographic coordinates for this address.</summary>
    public void SetCoordinates(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }
}
