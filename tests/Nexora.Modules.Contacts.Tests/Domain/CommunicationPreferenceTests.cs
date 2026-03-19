using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Tests.Domain;

public sealed class CommunicationPreferenceTests
{
    private readonly ContactId _contactId = ContactId.New();

    [Fact]
    public void Create_OptedIn_ShouldSetProperties()
    {
        // Arrange
        var pref = CommunicationPreference.Create(_contactId, CommunicationChannel.Email, true, "web_form");

        // Act & Assert
        pref.Id.Value.Should().NotBeEmpty();
        pref.ContactId.Should().Be(_contactId);
        pref.Channel.Should().Be(CommunicationChannel.Email);
        pref.OptedIn.Should().BeTrue();
        pref.OptedInAt.Should().NotBeNull();
        pref.OptedOutAt.Should().BeNull();
        pref.OptInSource.Should().Be("web_form");
    }

    [Fact]
    public void Create_OptedOut_ShouldSetTimestamp()
    {
        // Arrange
        var pref = CommunicationPreference.Create(_contactId, CommunicationChannel.Sms, false);

        // Act & Assert
        pref.OptedIn.Should().BeFalse();
        pref.OptedOutAt.Should().NotBeNull();
        pref.OptedInAt.Should().BeNull();
    }

    [Fact]
    public void OptIn_OptedOutPreference_ShouldUpdateState()
    {
        // Arrange
        var pref = CommunicationPreference.Create(_contactId, CommunicationChannel.WhatsApp, false);

        // Act
        pref.OptIn("verbal");

        // Assert
        pref.OptedIn.Should().BeTrue();
        pref.OptedInAt.Should().NotBeNull();
        pref.OptedOutAt.Should().BeNull();
        pref.OptInSource.Should().Be("verbal");
    }

    [Fact]
    public void OptOut_OptedInPreference_ShouldUpdateState()
    {
        // Arrange
        var pref = CommunicationPreference.Create(_contactId, CommunicationChannel.Email, true, "web_form");

        // Act
        pref.OptOut();

        // Assert
        pref.OptedIn.Should().BeFalse();
        pref.OptedOutAt.Should().NotBeNull();
    }

    [Fact]
    public void OptIn_AfterOptOut_ShouldClearOptOutTimestamp()
    {
        // Arrange
        var pref = CommunicationPreference.Create(_contactId, CommunicationChannel.Phone, true);
        pref.OptOut();

        // Act
        pref.OptIn("written");

        // Assert
        pref.OptedIn.Should().BeTrue();
        pref.OptedOutAt.Should().BeNull();
        pref.OptInSource.Should().Be("written");
    }
}
