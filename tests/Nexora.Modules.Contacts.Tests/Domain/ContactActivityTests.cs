using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Tests.Domain;

public sealed class ContactActivityTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var contactId = ContactId.New();
        var orgId = Guid.NewGuid();

        // Act
        var activity = ContactActivity.Create(
            contactId, orgId, "crm", "meeting", "Meeting with client", "{\"location\":\"office\"}");

        // Assert
        activity.Id.Value.Should().NotBeEmpty();
        activity.ContactId.Should().Be(contactId);
        activity.OrganizationId.Should().Be(orgId);
        activity.ModuleSource.Should().Be("crm");
        activity.ActivityType.Should().Be("meeting");
        activity.Summary.Should().Be("Meeting with client");
        activity.Details.Should().Be("{\"location\":\"office\"}");
        activity.OccurredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_ShouldRaiseDomainEvent()
    {
        // Arrange
        var activity = ContactActivity.Create(
            ContactId.New(), Guid.NewGuid(), "donations", "donation", "Donation received");

        // Act & Assert
        activity.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Contacts.Domain.Events.ContactActivityLoggedEvent>();
    }

    [Fact]
    public void Create_WithoutDetails_ShouldHaveNullDetails()
    {
        // Arrange
        var activity = ContactActivity.Create(
            ContactId.New(), Guid.NewGuid(), "education", "enrollment", "Student enrolled");

        // Act & Assert
        activity.Details.Should().BeNull();
    }
}
