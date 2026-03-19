using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Tests.Domain;

public sealed class ContactTagTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var contactId = ContactId.New();
        var tagId = TagId.New();
        var orgId = Guid.NewGuid();

        // Act
        var ct = ContactTag.Create(contactId, tagId, orgId);

        // Assert
        ct.Id.Value.Should().NotBeEmpty();
        ct.ContactId.Should().Be(contactId);
        ct.TagId.Should().Be(tagId);
        ct.OrganizationId.Should().Be(orgId);
        ct.AssignedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_MultipleTags_ShouldHaveDistinctIds()
    {
        // Arrange
        var ct1 = ContactTag.Create(ContactId.New(), TagId.New(), Guid.NewGuid());
        var ct2 = ContactTag.Create(ContactId.New(), TagId.New(), Guid.NewGuid());

        // Act & Assert
        ct1.Id.Should().NotBe(ct2.Id);
    }
}
