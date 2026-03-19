using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Tests.Domain;

public sealed class ContactNoteTests
{
    private readonly ContactId _contactId = ContactId.New();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var note = ContactNote.Create(_contactId, _userId, _orgId, "Important note about this contact");

        // Act & Assert
        note.Id.Value.Should().NotBeEmpty();
        note.ContactId.Should().Be(_contactId);
        note.AuthorUserId.Should().Be(_userId);
        note.OrganizationId.Should().Be(_orgId);
        note.Content.Should().Be("Important note about this contact");
        note.IsPinned.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldTrimContent()
    {
        // Arrange
        var note = ContactNote.Create(_contactId, _userId, _orgId, "  Some note  ");

        // Act & Assert
        note.Content.Should().Be("Some note");
    }

    [Fact]
    public void Update_ShouldChangeContent()
    {
        // Arrange
        var note = ContactNote.Create(_contactId, _userId, _orgId, "Original");

        // Act
        note.Update("Updated content");

        // Assert
        note.Content.Should().Be("Updated content");
    }

    [Fact]
    public void Pin_UnpinnedNote_ShouldSetPinned()
    {
        // Arrange
        var note = ContactNote.Create(_contactId, _userId, _orgId, "Test");

        // Act
        note.Pin();

        // Assert
        note.IsPinned.Should().BeTrue();
    }

    [Fact]
    public void Unpin_PinnedNote_ShouldClearPinned()
    {
        // Arrange
        var note = ContactNote.Create(_contactId, _userId, _orgId, "Test");
        note.Pin();

        // Act
        note.Unpin();

        // Assert
        note.IsPinned.Should().BeFalse();
    }
}
