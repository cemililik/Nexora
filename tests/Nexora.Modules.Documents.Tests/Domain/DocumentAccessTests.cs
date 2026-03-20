using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;

namespace Nexora.Modules.Documents.Tests.Domain;

public sealed class DocumentAccessTests
{
    [Fact]
    public void Create_WithUserId_ShouldSetProperties()
    {
        // Arrange
        var documentId = DocumentId.New();
        var userId = Guid.NewGuid();

        // Act
        var access = DocumentAccess.Create(documentId, userId, null, AccessPermission.View);

        // Assert
        access.Id.Value.Should().NotBeEmpty();
        access.DocumentId.Should().Be(documentId);
        access.UserId.Should().Be(userId);
        access.RoleId.Should().BeNull();
        access.Permission.Should().Be(AccessPermission.View);
    }

    [Fact]
    public void Create_WithRoleId_ShouldSetProperties()
    {
        // Arrange
        var documentId = DocumentId.New();
        var roleId = Guid.NewGuid();

        // Act
        var access = DocumentAccess.Create(documentId, null, roleId, AccessPermission.Manage);

        // Assert
        access.UserId.Should().BeNull();
        access.RoleId.Should().Be(roleId);
        access.Permission.Should().Be(AccessPermission.Manage);
    }

    [Fact]
    public void UpdatePermission_NewPermission_ShouldChangePermission()
    {
        // Arrange
        var access = DocumentAccess.Create(DocumentId.New(), Guid.NewGuid(), null, AccessPermission.View);

        // Act
        access.UpdatePermission(AccessPermission.Edit);

        // Assert
        access.Permission.Should().Be(AccessPermission.Edit);
    }
}
