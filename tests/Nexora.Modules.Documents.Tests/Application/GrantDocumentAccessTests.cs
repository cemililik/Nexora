using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class GrantDocumentAccessTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GrantDocumentAccessTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);

        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
    }

    private async Task<Guid> SeedDocumentAsync()
    {
        var folder = Folder.Create(_tenantId, _orgId, "TestFolder", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var document = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "test.pdf", "application/pdf", 1024, "key");
        await _dbContext.Documents.AddAsync(document);
        await _dbContext.SaveChangesAsync();
        return document.Id.Value;
    }

    [Fact]
    public async Task Handle_ValidUserAccess_ShouldGrantAccess()
    {
        // Arrange
        var docId = await SeedDocumentAsync();
        var userId = Guid.NewGuid();
        var handler = new GrantDocumentAccessHandler(_dbContext, _tenantAccessor, NullLogger<GrantDocumentAccessHandler>.Instance);
        var command = new GrantDocumentAccessCommand(docId, userId, null, "View");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UserId.Should().Be(userId);
        result.Value.Permission.Should().Be("View");
    }

    [Fact]
    public async Task Handle_ValidRoleAccess_ShouldGrantAccess()
    {
        // Arrange
        var docId = await SeedDocumentAsync();
        var roleId = Guid.NewGuid();
        var handler = new GrantDocumentAccessHandler(_dbContext, _tenantAccessor, NullLogger<GrantDocumentAccessHandler>.Instance);
        var command = new GrantDocumentAccessCommand(docId, null, roleId, "Edit");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.RoleId.Should().Be(roleId);
        result.Value.Permission.Should().Be("Edit");
    }

    [Fact]
    public async Task Handle_InvalidDocument_ShouldReturnFailure()
    {
        // Arrange
        var handler = new GrantDocumentAccessHandler(_dbContext, _tenantAccessor, NullLogger<GrantDocumentAccessHandler>.Instance);
        var command = new GrantDocumentAccessCommand(Guid.NewGuid(), Guid.NewGuid(), null, "View");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ManagePermission_ShouldSetManage()
    {
        // Arrange
        var docId = await SeedDocumentAsync();
        var handler = new GrantDocumentAccessHandler(_dbContext, _tenantAccessor, NullLogger<GrantDocumentAccessHandler>.Instance);
        var command = new GrantDocumentAccessCommand(docId, Guid.NewGuid(), null, "Manage");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Permission.Should().Be("Manage");
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
