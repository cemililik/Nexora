using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Documents.Tests.Domain;

public sealed class FolderTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void Create_ValidName_ShouldSetProperties()
    {
        // Arrange & Act
        var folder = Folder.Create(_tenantId, _orgId, "Reports", _userId);

        // Assert
        folder.Id.Value.Should().NotBeEmpty();
        folder.TenantId.Should().Be(_tenantId);
        folder.OrganizationId.Should().Be(_orgId);
        folder.Name.Should().Be("Reports");
        folder.Path.Should().Be("/Reports");
        folder.OwnerUserId.Should().Be(_userId);
        folder.IsSystem.Should().BeFalse();
        folder.ParentFolderId.Should().BeNull();
    }

    [Fact]
    public void Create_WithParent_ShouldBuildPath()
    {
        // Arrange & Act
        var parentId = FolderId.New();
        var folder = Folder.Create(_tenantId, _orgId, "SubFolder", _userId, "/Root", parentId);

        // Assert
        folder.Path.Should().Be("/Root/SubFolder");
        folder.ParentFolderId.Should().Be(parentId);
    }

    [Fact]
    public void Create_NameWithWhitespace_ShouldTrimName()
    {
        // Arrange & Act
        var folder = Folder.Create(_tenantId, _orgId, "  Trimmed  ", _userId);

        // Assert
        folder.Name.Should().Be("Trimmed");
        folder.Path.Should().Be("/Trimmed");
    }

    [Fact]
    public void Create_ValidFolder_ShouldRaiseDomainEvent()
    {
        // Arrange & Act
        var folder = Folder.Create(_tenantId, _orgId, "Events", _userId);

        // Assert
        folder.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Documents.Domain.Events.FolderCreatedEvent>();
    }

    [Fact]
    public void Create_AsSystem_ShouldSetIsSystem()
    {
        // Arrange & Act
        var folder = Folder.Create(_tenantId, _orgId, "System", _userId, isSystem: true);

        // Assert
        folder.IsSystem.Should().BeTrue();
    }

    [Fact]
    public void Create_WithModule_ShouldSetModuleFields()
    {
        // Arrange
        var moduleRef = Guid.NewGuid();

        // Act
        var folder = Folder.Create(_tenantId, _orgId, "CRM Docs", _userId, moduleName: "crm", moduleRef: moduleRef);

        // Assert
        folder.ModuleName.Should().Be("crm");
        folder.ModuleRef.Should().Be(moduleRef);
    }

    [Fact]
    public void Rename_ValidName_ShouldChangeName()
    {
        // Arrange
        var folder = Folder.Create(_tenantId, _orgId, "OldName", _userId);

        // Act
        folder.Rename("NewName");

        // Assert
        folder.Name.Should().Be("NewName");
    }

    [Fact]
    public void Rename_SystemFolder_ShouldThrow()
    {
        // Arrange
        var folder = Folder.Create(_tenantId, _orgId, "System", _userId, isSystem: true);

        // Act
        var act = () => folder.Rename("NewName");

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MoveTo_NewParent_ShouldUpdateParentAndPath()
    {
        // Arrange
        var folder = Folder.Create(_tenantId, _orgId, "Movable", _userId);
        var newParentId = FolderId.New();

        // Act
        folder.MoveTo(newParentId, "/NewParent/Movable");

        // Assert
        folder.ParentFolderId.Should().Be(newParentId);
        folder.Path.Should().Be("/NewParent/Movable");
    }

    [Fact]
    public void MoveTo_SystemFolder_ShouldThrow()
    {
        // Arrange
        var folder = Folder.Create(_tenantId, _orgId, "System", _userId, isSystem: true);

        // Act
        var act = () => folder.MoveTo(FolderId.New(), "/Other/System");

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UpdatePath_NewPath_ShouldChangePath()
    {
        // Arrange
        var folder = Folder.Create(_tenantId, _orgId, "Test", _userId);

        // Act
        folder.UpdatePath("/New/Path");

        // Assert
        folder.Path.Should().Be("/New/Path");
    }
}
