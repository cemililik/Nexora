using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Tests.Domain;

public sealed class ConsentRecordTests
{
    private readonly ContactId _contactId = ContactId.New();

    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var consent = ConsentRecord.Create(
            _contactId, ConsentType.EmailMarketing, true, "web_form", "192.168.1.1");

        // Act & Assert
        consent.Id.Value.Should().NotBeEmpty();
        consent.ContactId.Should().Be(_contactId);
        consent.ConsentType.Should().Be(ConsentType.EmailMarketing);
        consent.Granted.Should().BeTrue();
        consent.Source.Should().Be("web_form");
        consent.IpAddress.Should().Be("192.168.1.1");
        consent.GrantedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        consent.RevokedAt.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldRaiseDomainEvent()
    {
        // Arrange
        var consent = ConsentRecord.Create(
            _contactId, ConsentType.DataProcessing, true);

        // Act & Assert
        consent.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Contacts.Domain.Events.ConsentChangedEvent>();
    }

    [Fact]
    public void Revoke_GrantedConsent_ShouldSetRevokedState()
    {
        // Arrange
        var consent = ConsentRecord.Create(
            _contactId, ConsentType.SmsMarketing, true);
        consent.ClearDomainEvents();

        // Act
        consent.Revoke();

        // Assert
        consent.Granted.Should().BeFalse();
        consent.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public void Revoke_GrantedConsent_ShouldRaiseDomainEvent()
    {
        // Arrange
        var consent = ConsentRecord.Create(
            _contactId, ConsentType.EmailMarketing, true);
        consent.ClearDomainEvents();

        // Act
        consent.Revoke();

        // Assert
        consent.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Contacts.Domain.Events.ConsentChangedEvent>();
    }

    [Fact]
    public void Create_NotGranted_ShouldSetGrantedFalse()
    {
        // Arrange
        var consent = ConsentRecord.Create(
            _contactId, ConsentType.DataProcessing, false);

        // Act & Assert
        consent.Granted.Should().BeFalse();
    }
}
