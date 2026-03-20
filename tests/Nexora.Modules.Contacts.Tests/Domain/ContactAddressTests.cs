using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Tests.Domain;

public sealed class ContactAddressTests
{
    private readonly ContactId _contactId = ContactId.New();

    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var address = ContactAddress.Create(
            _contactId, AddressType.Home, "123 Main St", "Istanbul", "TR",
            street2: "Apt 4", state: "Istanbul", postalCode: "34000");

        // Act & Assert
        address.Id.Value.Should().NotBeEmpty();
        address.ContactId.Should().Be(_contactId);
        address.Type.Should().Be(AddressType.Home);
        address.Street1.Should().Be("123 Main St");
        address.Street2.Should().Be("Apt 4");
        address.City.Should().Be("Istanbul");
        address.State.Should().Be("Istanbul");
        address.PostalCode.Should().Be("34000");
        address.CountryCode.Should().Be("TR");
        address.IsPrimary.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldNormalizeCountryCode()
    {
        // Arrange
        var address = ContactAddress.Create(
            _contactId, AddressType.Work, "456 Elm St", "Ankara", "tr");

        // Act & Assert
        address.CountryCode.Should().Be("TR");
    }

    [Fact]
    public void Update_ShouldChangeProperties()
    {
        // Arrange
        var address = ContactAddress.Create(
            _contactId, AddressType.Home, "Old St", "OldCity", "US");

        // Act
        address.Update(AddressType.Work, "New St", "NewCity", "TR",
            street2: "Floor 3", state: "Ankara", postalCode: "06000");

        // Assert
        address.Type.Should().Be(AddressType.Work);
        address.Street1.Should().Be("New St");
        address.City.Should().Be("NewCity");
        address.CountryCode.Should().Be("TR");
    }

    [Fact]
    public void SetPrimary_WhenCalled_ShouldToggle()
    {
        // Arrange
        var address = ContactAddress.Create(
            _contactId, AddressType.Home, "Main St", "City", "US");

        // Act & Assert
        address.SetPrimary(true);
        address.IsPrimary.Should().BeTrue();

        address.SetPrimary(false);
        address.IsPrimary.Should().BeFalse();
    }

    [Fact]
    public void SetCoordinates_WithValidLatLong_ShouldSetValues()
    {
        // Arrange
        var address = ContactAddress.Create(
            _contactId, AddressType.Home, "Main St", "Istanbul", "TR");

        // Act
        address.SetCoordinates(41.0082, 28.9784);

        // Assert
        address.Latitude.Should().Be(41.0082);
        address.Longitude.Should().Be(28.9784);
    }
}
