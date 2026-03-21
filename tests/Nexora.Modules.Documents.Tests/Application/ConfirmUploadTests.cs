using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using NSubstitute;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class ConfirmUploadTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly IFileStorageService _fileStorage = Substitute.For<IFileStorageService>();
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IOptions<StorageOptions> _storageOptions;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public ConfirmUploadTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        _storageOptions = Options.Create(new StorageOptions { BucketPrefix = "nexora" });

        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
    }

    private async Task<Guid> SeedFolderAsync()
    {
        var folder = Folder.Create(_tenantId, _orgId, "TestFolder", _userId);
        await _dbContext.Folders.AddAsync(folder);
        await _dbContext.SaveChangesAsync();
        return folder.Id.Value;
    }

    [Fact]
    public async Task Handle_ValidUpload_CreatesDocument()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        var storageKey = $"{_orgId}/documents/{Guid.NewGuid()}/report.pdf";
        _fileStorage.ObjectExistsAsync(Arg.Any<string>(), storageKey, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreateHandler();
        var command = new ConfirmUploadCommand(folderId, storageKey, "report.pdf", "application/pdf", 1024);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("report.pdf");
        result.Value.MimeType.Should().Be("application/pdf");
        result.Value.FileSize.Should().Be(1024);
        result.Value.StorageKey.Should().Be(storageKey);
        result.Value.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Handle_InvalidFolder_ReturnsFailure()
    {
        // Arrange
        _fileStorage.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreateHandler();
        var command = new ConfirmUploadCommand(Guid.NewGuid(), "key", "test.pdf", "application/pdf", 100);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ObjectNotInStorage_ReturnsFailure()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        _fileStorage.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = CreateHandler();
        var command = new ConfirmUploadCommand(folderId, "missing-key", "test.pdf", "application/pdf", 100);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidUpload_PersistsToDatabase()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        var storageKey = "some/key";
        _fileStorage.ObjectExistsAsync(Arg.Any<string>(), storageKey, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreateHandler();
        var command = new ConfirmUploadCommand(folderId, storageKey, "persist.pdf", "application/pdf", 100);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var count = await _dbContext.Documents.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithDescription_SetsDescription()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        _fileStorage.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreateHandler();
        var command = new ConfirmUploadCommand(folderId, "key", "doc.pdf", "application/pdf", 500,
            Description: "A test document");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Description.Should().Be("A test document");
    }

    [Fact]
    public async Task Handle_WithLinkedEntity_SetsEntityFields()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        var entityId = Guid.NewGuid();
        _fileStorage.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreateHandler();
        var command = new ConfirmUploadCommand(folderId, "key", "linked.pdf", "application/pdf", 300,
            LinkedEntityId: entityId, LinkedEntityType: "Contact");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.LinkedEntityId.Should().Be(entityId);
        result.Value.LinkedEntityType.Should().Be("Contact");
    }

    [Fact]
    public async Task Handle_FolderInDifferentOrganization_ReturnsFailure()
    {
        // Arrange — folder belongs to a different organization
        var otherOrgId = Guid.NewGuid();
        var folder = Folder.Create(_tenantId, otherOrgId, "OtherOrgFolder", _userId);
        await _dbContext.Folders.AddAsync(folder);
        await _dbContext.SaveChangesAsync();

        _fileStorage.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreateHandler();
        var command = new ConfirmUploadCommand(folder.Id.Value, "key", "test.pdf", "application/pdf", 100);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidUpload_UsesTenantScopedBucket()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        var storageKey = "some/key";
        _fileStorage.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreateHandler();
        var command = new ConfirmUploadCommand(folderId, storageKey, "test.pdf", "application/pdf", 100);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        await _fileStorage.Received(1).ObjectExistsAsync(
            $"nexora-{_tenantId}",
            storageKey,
            Arg.Any<CancellationToken>());
    }

    public void Dispose() => _dbContext.Dispose();

    private ConfirmUploadHandler CreateHandler() =>
        new(_dbContext, _fileStorage, _tenantAccessor, _storageOptions, NullLogger<ConfirmUploadHandler>.Instance);

    private ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString(), _userId.ToString());
        return accessor;
    }
}
