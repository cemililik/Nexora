using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.Events;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Documents.Tests.Domain;

public sealed class DocumentTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly FolderId _folderId = FolderId.New();
    private readonly Guid _userId = Guid.NewGuid();

    private Document CreateDocument(string name = "test.pdf") =>
        Document.Create(_tenantId, _orgId, _folderId, _userId, name, "application/pdf", 1024, "storage/test.pdf");

    [Fact]
    public void Create_ValidInput_ShouldSetProperties()
    {
        // Arrange & Act
        var doc = CreateDocument();

        // Assert
        doc.Id.Value.Should().NotBeEmpty();
        doc.TenantId.Should().Be(_tenantId);
        doc.FolderId.Should().Be(_folderId);
        doc.Name.Should().Be("test.pdf");
        doc.MimeType.Should().Be("application/pdf");
        doc.FileSize.Should().Be(1024);
        doc.StorageKey.Should().Be("storage/test.pdf");
        doc.Status.Should().Be(DocumentStatus.Active);
        doc.CurrentVersion.Should().Be(1);
    }

    [Fact]
    public void Create_ValidDocument_ShouldRaiseDomainEvent()
    {
        // Arrange & Act
        var doc = CreateDocument();

        // Assert
        doc.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DocumentCreatedEvent>();
    }

    [Fact]
    public void Create_NameWithWhitespace_ShouldTrimName()
    {
        // Arrange & Act
        var doc = Document.Create(_tenantId, _orgId, _folderId, _userId, "  trimmed.pdf  ", "application/pdf", 100, "key");

        // Assert
        doc.Name.Should().Be("trimmed.pdf");
    }

    [Fact]
    public void AddVersion_ShouldIncrementVersion()
    {
        // Arrange
        var doc = CreateDocument();
        doc.ClearDomainEvents();

        // Act
        var version = doc.AddVersion("storage/v2.pdf", 2048, _userId, "Updated content");

        // Assert
        doc.CurrentVersion.Should().Be(2);
        doc.StorageKey.Should().Be("storage/v2.pdf");
        doc.FileSize.Should().Be(2048);
        doc.Versions.Should().ContainSingle();
    }

    [Fact]
    public void AddVersion_ShouldRaiseDomainEvent()
    {
        // Arrange
        var doc = CreateDocument();
        doc.ClearDomainEvents();

        // Act
        doc.AddVersion("storage/v2.pdf", 2048, _userId);

        // Assert
        doc.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DocumentVersionAddedEvent>();
    }

    [Fact]
    public void Archive_ActiveDocument_ShouldChangeStatus()
    {
        // Arrange
        var doc = CreateDocument();
        doc.ClearDomainEvents();

        // Act
        doc.Archive();

        // Assert
        doc.Status.Should().Be(DocumentStatus.Archived);
    }

    [Fact]
    public void Archive_ActiveDocument_ShouldRaiseDomainEvent()
    {
        // Arrange
        var doc = CreateDocument();
        doc.ClearDomainEvents();

        // Act
        doc.Archive();

        // Assert
        doc.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DocumentArchivedEvent>();
    }

    [Fact]
    public void Archive_WhenDeleted_ShouldThrow()
    {
        // Arrange
        var doc = CreateDocument();
        doc.SoftDelete();

        // Act
        var act = () => doc.Archive();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Archive_WhenAlreadyArchived_ShouldThrow()
    {
        // Arrange
        var doc = CreateDocument();
        doc.Archive();

        // Act
        var act = () => doc.Archive();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Restore_ShouldChangeStatusToActive()
    {
        // Arrange
        var doc = CreateDocument();
        doc.Archive();
        doc.ClearDomainEvents();

        // Act
        doc.Restore();

        // Assert
        doc.Status.Should().Be(DocumentStatus.Active);
    }

    [Fact]
    public void Restore_WhenNotArchived_ShouldThrow()
    {
        // Arrange
        var doc = CreateDocument();

        // Act
        var act = () => doc.Restore();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SoftDelete_ActiveDocument_ShouldSetDeletedStatus()
    {
        // Arrange
        var doc = CreateDocument();

        // Act
        doc.SoftDelete();

        // Assert
        doc.Status.Should().Be(DocumentStatus.Deleted);
    }

    [Fact]
    public void UpdateMetadata_NewValues_ShouldChangeFields()
    {
        // Arrange
        var doc = CreateDocument();

        // Act
        doc.UpdateMetadata("updated.pdf", "New description", "[\"tag1\"]");

        // Assert
        doc.Name.Should().Be("updated.pdf");
        doc.Description.Should().Be("New description");
        doc.Tags.Should().Be("[\"tag1\"]");
    }

    [Fact]
    public void MoveToFolder_NewFolder_ShouldChangeFolderId()
    {
        // Arrange
        var doc = CreateDocument();
        var newFolderId = FolderId.New();

        // Act
        doc.MoveToFolder(newFolderId);

        // Assert
        doc.FolderId.Should().Be(newFolderId);
    }

    [Fact]
    public void LinkToEntity_ValidEntity_ShouldSetEntityFields()
    {
        // Arrange
        var doc = CreateDocument();
        var entityId = Guid.NewGuid();

        // Act
        doc.LinkToEntity(entityId, "Contact");

        // Assert
        doc.LinkedEntityId.Should().Be(entityId);
        doc.LinkedEntityType.Should().Be("Contact");
    }

    [Fact]
    public void UnlinkEntity_LinkedDocument_ShouldClearEntityFields()
    {
        // Arrange
        var doc = CreateDocument();
        doc.LinkToEntity(Guid.NewGuid(), "Contact");

        // Act
        doc.UnlinkEntity();

        // Assert
        doc.LinkedEntityId.Should().BeNull();
        doc.LinkedEntityType.Should().BeNull();
    }

    [Fact]
    public void GrantAccess_WithUserId_ShouldAddAccess()
    {
        // Arrange
        var doc = CreateDocument();
        doc.ClearDomainEvents();
        var userId = Guid.NewGuid();

        // Act
        var access = doc.GrantAccess(userId, null, AccessPermission.View);

        // Assert
        doc.AccessList.Should().ContainSingle();
        access.UserId.Should().Be(userId);
        access.Permission.Should().Be(AccessPermission.View);
    }

    [Fact]
    public void GrantAccess_WithoutUserOrRole_ShouldThrow()
    {
        // Arrange
        var doc = CreateDocument();

        // Act
        var act = () => doc.GrantAccess(null, null, AccessPermission.View);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void GrantAccess_ShouldRaiseDomainEvent()
    {
        // Arrange
        var doc = CreateDocument();
        doc.ClearDomainEvents();

        // Act
        doc.GrantAccess(Guid.NewGuid(), null, AccessPermission.Edit);

        // Assert
        doc.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DocumentAccessGrantedEvent>();
    }
}
