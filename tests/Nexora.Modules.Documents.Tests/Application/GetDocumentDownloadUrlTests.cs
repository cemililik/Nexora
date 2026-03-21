using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Queries;
using Nexora.Modules.Documents.Application.Services;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using NSubstitute;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class GetDocumentDownloadUrlTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly IFileStorageService _fileStorage = Substitute.For<IFileStorageService>();
    private readonly IDocumentAccessChecker _accessChecker = Substitute.For<IDocumentAccessChecker>();
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IOptions<StorageOptions> _storageOptions;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GetDocumentDownloadUrlTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        _storageOptions = Options.Create(new StorageOptions
        {
            BucketPrefix = "nexora",
            DefaultPresignedUrlExpiry = TimeSpan.FromMinutes(15)
        });

        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DocumentsDbContext(options, _tenantAccessor);

        // Default: allow access
        _accessChecker.HasAccessAsync(
                Arg.Any<DocumentId>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private async Task<Guid> SeedDocumentAsync(string storageKey = "storage/test.pdf")
    {
        var folder = Folder.Create(_tenantId, _orgId, "TestFolder", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var document = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "test.pdf", "application/pdf", 1024, storageKey);
        await _dbContext.Documents.AddAsync(document);
        await _dbContext.SaveChangesAsync();
        return document.Id.Value;
    }

    [Fact]
    public async Task Handle_ExistingDocument_ReturnsDownloadUrl()
    {
        // Arrange
        var storageKey = $"{_orgId}/documents/test.pdf";
        var docId = await SeedDocumentAsync(storageKey);
        var expectedUrl = "https://minio.local/presigned-download";
        _fileStorage.GenerateDownloadPresignedUrlAsync(
                Arg.Any<string>(), storageKey, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new PresignedUrlResult(expectedUrl, DateTimeOffset.UtcNow.AddMinutes(15)));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetDocumentDownloadUrlQuery(docId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DownloadUrl.Should().Be(expectedUrl);
        result.Value.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Handle_NonExistentDocument_ReturnsFailure()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetDocumentDownloadUrlQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ExistingDocument_UsesTenantScopedBucket()
    {
        // Arrange
        var storageKey = "org/documents/file.pdf";
        var docId = await SeedDocumentAsync(storageKey);
        _fileStorage.GenerateDownloadPresignedUrlAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new PresignedUrlResult("https://url", DateTimeOffset.UtcNow.AddMinutes(15)));

        var handler = CreateHandler();

        // Act
        await handler.Handle(new GetDocumentDownloadUrlQuery(docId), CancellationToken.None);

        // Assert
        await _fileStorage.Received(1).GenerateDownloadPresignedUrlAsync(
            $"nexora-{_tenantId}",
            storageKey,
            TimeSpan.FromMinutes(15),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DifferentTenantDocument_ReturnsFailure()
    {
        // Arrange — seed document with different tenant
        var otherTenantId = Guid.NewGuid();
        var folder = Folder.Create(otherTenantId, Guid.NewGuid(), "OtherFolder", Guid.NewGuid());
        await _dbContext.Folders.AddAsync(folder);
        var document = Document.Create(otherTenantId, Guid.NewGuid(), folder.Id, Guid.NewGuid(),
            "other.pdf", "application/pdf", 100, "key");
        await _dbContext.Documents.AddAsync(document);
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetDocumentDownloadUrlQuery(document.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonExistentDocument_DoesNotCallStorage()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        await handler.Handle(new GetDocumentDownloadUrlQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await _fileStorage.DidNotReceive().GenerateDownloadPresignedUrlAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AccessDenied_ReturnsFailure()
    {
        // Arrange
        var docId = await SeedDocumentAsync();

        var localAccessChecker = Substitute.For<IDocumentAccessChecker>();
        localAccessChecker.HasAccessAsync(
                Arg.Any<DocumentId>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new GetDocumentDownloadUrlHandler(
            _dbContext, _fileStorage, localAccessChecker, _tenantAccessor, _storageOptions, NullLogger<GetDocumentDownloadUrlHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetDocumentDownloadUrlQuery(docId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_documents_error_access_denied");
    }

    [Fact]
    public async Task Handle_AccessDenied_DoesNotCallStorage()
    {
        // Arrange
        var docId = await SeedDocumentAsync();

        var localAccessChecker = Substitute.For<IDocumentAccessChecker>();
        localAccessChecker.HasAccessAsync(
                Arg.Any<DocumentId>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new GetDocumentDownloadUrlHandler(
            _dbContext, _fileStorage, localAccessChecker, _tenantAccessor, _storageOptions, NullLogger<GetDocumentDownloadUrlHandler>.Instance);

        // Act
        await handler.Handle(new GetDocumentDownloadUrlQuery(docId), CancellationToken.None);

        // Assert
        await _fileStorage.DidNotReceive().GenerateDownloadPresignedUrlAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    public void Dispose() => _dbContext.Dispose();

    private GetDocumentDownloadUrlHandler CreateHandler() =>
        new(_dbContext, _fileStorage, _accessChecker, _tenantAccessor, _storageOptions, NullLogger<GetDocumentDownloadUrlHandler>.Instance);

    private ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString(), _userId.ToString());
        return accessor;
    }
}
