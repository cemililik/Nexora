using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class CreateFolderTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public CreateFolderTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);

        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidFolder_ShouldCreateFolder()
    {
        // Arrange
        var handler = new CreateFolderHandler(_dbContext, _tenantAccessor, NullLogger<CreateFolderHandler>.Instance);
        var command = new CreateFolderCommand("Reports");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Reports");
        result.Value.Path.Should().Be("/Reports");
        result.Value.IsSystem.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithParentFolder_ShouldBuildNestedPath()
    {
        // Arrange
        var handler = new CreateFolderHandler(_dbContext, _tenantAccessor, NullLogger<CreateFolderHandler>.Instance);
        var parentResult = await handler.Handle(new CreateFolderCommand("Parent"), CancellationToken.None);
        var command = new CreateFolderCommand("Child", parentResult.Value!.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Path.Should().Be("/Parent/Child");
        result.Value.ParentFolderId.Should().Be(parentResult.Value.Id);
    }

    [Fact]
    public async Task Handle_WithInvalidParent_ShouldReturnFailure()
    {
        // Arrange
        var handler = new CreateFolderHandler(_dbContext, _tenantAccessor, NullLogger<CreateFolderHandler>.Instance);
        var command = new CreateFolderCommand("Orphan", Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldPersistToDatabase()
    {
        // Arrange
        var handler = new CreateFolderHandler(_dbContext, _tenantAccessor, NullLogger<CreateFolderHandler>.Instance);
        var command = new CreateFolderCommand("Persisted");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var count = await _dbContext.Folders.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SystemFolder_ShouldSetIsSystem()
    {
        // Arrange
        var handler = new CreateFolderHandler(_dbContext, _tenantAccessor, NullLogger<CreateFolderHandler>.Instance);
        var command = new CreateFolderCommand("SystemFolder", IsSystem: true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsSystem.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MultipleFolders_ShouldHaveDistinctIds()
    {
        // Arrange
        var handler = new CreateFolderHandler(_dbContext, _tenantAccessor, NullLogger<CreateFolderHandler>.Instance);

        // Act
        var r1 = await handler.Handle(new CreateFolderCommand("A"), CancellationToken.None);
        var r2 = await handler.Handle(new CreateFolderCommand("B"), CancellationToken.None);

        // Assert
        r1.Value!.Id.Should().NotBe(r2.Value!.Id);
    }

    [Fact]
    public async Task Handle_WithModuleScope_ShouldPersistModuleFields()
    {
        // Arrange
        var handler = new CreateFolderHandler(_dbContext, _tenantAccessor, NullLogger<CreateFolderHandler>.Instance);
        var moduleRef = Guid.NewGuid();
        var command = new CreateFolderCommand("ModuleFolder", ModuleName: "CRM", ModuleRef: moduleRef);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ModuleName.Should().Be("CRM");

        var persisted = await _dbContext.Folders
            .AsNoTracking()
            .FirstAsync(f => f.Id == FolderId.From(result.Value.Id));
        persisted.ModuleName.Should().Be("CRM");
        persisted.ModuleRef.Should().Be(moduleRef);
    }

    [Fact]
    public async Task Handle_SystemFolderWithParent_ShouldSetFlagsAndPath()
    {
        // Arrange
        var handler = new CreateFolderHandler(_dbContext, _tenantAccessor, NullLogger<CreateFolderHandler>.Instance);
        var parentResult = await handler.Handle(new CreateFolderCommand("Parent"), CancellationToken.None);
        var command = new CreateFolderCommand("SystemChild", parentResult.Value!.Id, IsSystem: true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsSystem.Should().BeTrue();
        result.Value.Path.Should().StartWith("/Parent/");
        result.Value.Path.Should().EndWith("SystemChild");
        result.Value.ParentFolderId.Should().Be(parentResult.Value.Id);
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
