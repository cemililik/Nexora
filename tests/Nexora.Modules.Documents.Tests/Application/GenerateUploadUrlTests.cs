using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using NSubstitute;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class GenerateUploadUrlTests
{
    private readonly IFileStorageService _fileStorage = Substitute.For<IFileStorageService>();
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IOptions<StorageOptions> _storageOptions;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GenerateUploadUrlTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        _storageOptions = Options.Create(new StorageOptions
        {
            BucketPrefix = "nexora",
            DefaultPresignedUrlExpiry = TimeSpan.FromMinutes(15)
        });
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsUploadUrl()
    {
        // Arrange
        var expectedUrl = "https://minio.local/presigned-upload";
        _fileStorage.GenerateUploadPresignedUrlAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new PresignedUrlResult(expectedUrl, DateTimeOffset.UtcNow.AddMinutes(15)));

        var handler = CreateHandler();
        var command = new GenerateUploadUrlCommand("report.pdf", "application/pdf", 1024);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UploadUrl.Should().Be(expectedUrl);
        result.Value.StorageKey.Should().Contain("report.pdf");
        result.Value.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Handle_ValidRequest_GeneratesTenantScopedBucket()
    {
        // Arrange
        _fileStorage.GenerateUploadPresignedUrlAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new PresignedUrlResult("https://url", DateTimeOffset.UtcNow.AddMinutes(15)));

        var handler = CreateHandler();
        var command = new GenerateUploadUrlCommand("test.pdf", "application/pdf", 100);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        await _fileStorage.Received(1).GenerateUploadPresignedUrlAsync(
            $"nexora-{_tenantId}",
            Arg.Is<string>(k => k.StartsWith($"{_orgId}/documents/")),
            "application/pdf",
            TimeSpan.FromMinutes(15),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_IncludesOrgIdInStorageKey()
    {
        // Arrange
        _fileStorage.GenerateUploadPresignedUrlAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new PresignedUrlResult("https://url", DateTimeOffset.UtcNow.AddMinutes(15)));

        var handler = CreateHandler();
        var command = new GenerateUploadUrlCommand("doc.pdf", "application/pdf", 500);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Value!.StorageKey.Should().StartWith($"{_orgId}/documents/");
        result.Value.StorageKey.Should().EndWith("/doc.pdf");
    }

    [Fact]
    public async Task Handle_InvalidTenantContext_ThrowsException()
    {
        // Arrange
        var accessor = new TenantContextAccessor();
        // No tenant set — accessing Current throws
        var handler = new GenerateUploadUrlHandler(
            _fileStorage, accessor, _storageOptions, NullLogger<GenerateUploadUrlHandler>.Instance);
        var command = new GenerateUploadUrlCommand("test.pdf", "application/pdf", 100);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Handle_ValidRequest_PassesContentTypeToStorage()
    {
        // Arrange
        _fileStorage.GenerateUploadPresignedUrlAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new PresignedUrlResult("https://url", DateTimeOffset.UtcNow.AddMinutes(15)));

        var handler = CreateHandler();
        var command = new GenerateUploadUrlCommand("image.png", "image/png", 2048);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        await _fileStorage.Received(1).GenerateUploadPresignedUrlAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            "image/png",
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MultipleRequests_GeneratesUniqueStorageKeys()
    {
        // Arrange
        _fileStorage.GenerateUploadPresignedUrlAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new PresignedUrlResult("https://url", DateTimeOffset.UtcNow.AddMinutes(15)));

        var handler = CreateHandler();
        var command = new GenerateUploadUrlCommand("test.pdf", "application/pdf", 100);

        // Act
        var result1 = await handler.Handle(command, CancellationToken.None);
        var result2 = await handler.Handle(command, CancellationToken.None);

        // Assert
        result1.Value!.StorageKey.Should().NotBe(result2.Value!.StorageKey);
    }

    private GenerateUploadUrlHandler CreateHandler() =>
        new(_fileStorage, _tenantAccessor, _storageOptions, NullLogger<GenerateUploadUrlHandler>.Instance);

    private ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString(), _userId.ToString());
        return accessor;
    }
}
