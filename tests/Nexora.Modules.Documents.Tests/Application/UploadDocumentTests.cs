using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class UploadDocumentTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public UploadDocumentTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);

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
    public async Task Handle_ValidDocument_ShouldCreateDocument()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        var handler = new UploadDocumentHandler(_dbContext, _tenantAccessor, NullLogger<UploadDocumentHandler>.Instance);
        var command = new UploadDocumentCommand(folderId, "report.pdf", "application/pdf", 1024, "storage/report.pdf");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("report.pdf");
        result.Value.MimeType.Should().Be("application/pdf");
        result.Value.FileSize.Should().Be(1024);
        result.Value.Status.Should().Be("Active");
        result.Value.CurrentVersion.Should().Be(1);
    }

    [Fact]
    public async Task Handle_InvalidFolder_ShouldReturnFailure()
    {
        // Arrange
        var handler = new UploadDocumentHandler(_dbContext, _tenantAccessor, NullLogger<UploadDocumentHandler>.Instance);
        var command = new UploadDocumentCommand(Guid.NewGuid(), "test.pdf", "application/pdf", 100, "key");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithDescription_ShouldSetDescription()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        var handler = new UploadDocumentHandler(_dbContext, _tenantAccessor, NullLogger<UploadDocumentHandler>.Instance);
        var command = new UploadDocumentCommand(folderId, "doc.pdf", "application/pdf", 500, "key", Description: "A test doc");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Description.Should().Be("A test doc");
    }

    [Fact]
    public async Task Handle_WithLinkedEntity_ShouldSetEntityFields()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        var entityId = Guid.NewGuid();
        var handler = new UploadDocumentHandler(_dbContext, _tenantAccessor, NullLogger<UploadDocumentHandler>.Instance);
        var command = new UploadDocumentCommand(folderId, "linked.pdf", "application/pdf", 300, "key",
            LinkedEntityId: entityId, LinkedEntityType: "Contact");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.LinkedEntityId.Should().Be(entityId);
        result.Value.LinkedEntityType.Should().Be("Contact");
    }

    [Fact]
    public async Task Handle_ShouldPersistToDatabase()
    {
        // Arrange
        var folderId = await SeedFolderAsync();
        var handler = new UploadDocumentHandler(_dbContext, _tenantAccessor, NullLogger<UploadDocumentHandler>.Instance);
        var command = new UploadDocumentCommand(folderId, "persist.pdf", "application/pdf", 100, "key");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var count = await _dbContext.Documents.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public void Validate_ZeroFileSize_ShouldFail()
    {
        // Arrange
        var validator = new UploadDocumentValidator();
        var command = new UploadDocumentCommand(Guid.NewGuid(), "test.pdf", "application/pdf", 0, "storage/key");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FileSize");
    }

    [Fact]
    public void Validate_NegativeFileSize_ShouldFail()
    {
        // Arrange
        var validator = new UploadDocumentValidator();
        var command = new UploadDocumentCommand(Guid.NewGuid(), "test.pdf", "application/pdf", -1, "storage/key");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FileSize");
    }

    [Fact]
    public void Validate_ExceedsMaxFileSize_ShouldFail()
    {
        // Arrange
        var validator = new UploadDocumentValidator();
        var command = new UploadDocumentCommand(Guid.NewGuid(), "test.pdf", "application/pdf", 52_428_801, "storage/key");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FileSize");
    }

    [Fact]
    public void Validate_EmptyName_ShouldFail()
    {
        // Arrange
        var validator = new UploadDocumentValidator();
        var command = new UploadDocumentCommand(Guid.NewGuid(), "", "application/pdf", 1024, "storage/key");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_EmptyStorageKey_ShouldFail()
    {
        // Arrange
        var validator = new UploadDocumentValidator();
        var command = new UploadDocumentCommand(Guid.NewGuid(), "test.pdf", "application/pdf", 1024, "");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StorageKey");
    }

    public void Dispose() => _dbContext.Dispose();

    private ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString(), _userId.ToString());
        return accessor;
    }
}
