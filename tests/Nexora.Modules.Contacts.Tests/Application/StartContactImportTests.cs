using Hangfire;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using NSubstitute;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class StartContactImportTests
{
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IFileStorageService _fileStorageService;
    private readonly IOptions<StorageOptions> _storageOptions;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public StartContactImportTests()
    {
        _tenantAccessor = CreateTenantAccessor(Guid.NewGuid(), _orgId);
        _fileStorageService = Substitute.For<IFileStorageService>();
        _storageOptions = Options.Create(new StorageOptions());
        _backgroundJobClient = Substitute.For<IBackgroundJobClient>();
    }

    [Fact]
    public async Task Handle_ValidCommand_FileExists_ShouldReturnQueuedJob()
    {
        // Arrange
        _fileStorageService.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new StartContactImportHandler(
            _fileStorageService, _tenantAccessor, _storageOptions,
            _backgroundJobClient, NullLogger<StartContactImportHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new StartContactImportCommand("contacts.csv", "csv", $"{_orgId}/contacts/imports/abc/contacts.csv"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Queued");
        result.Value.JobId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_FileNotInStorage_ShouldReturnFailure()
    {
        // Arrange
        _fileStorageService.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var handler = new StartContactImportHandler(
            _fileStorageService, _tenantAccessor, _storageOptions,
            _backgroundJobClient, NullLogger<StartContactImportHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new StartContactImportCommand("contacts.csv", "csv", $"{_orgId}/contacts/imports/abc/contacts.csv"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_import_file_not_found");
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldReturnCorrectTimestamp()
    {
        // Arrange
        _fileStorageService.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new StartContactImportHandler(
            _fileStorageService, _tenantAccessor, _storageOptions,
            _backgroundJobClient, NullLogger<StartContactImportHandler>.Instance);

        // Act
        var before = DateTimeOffset.UtcNow;
        var result = await handler.Handle(
            new StartContactImportCommand("data.xlsx", "xlsx", $"{_orgId}/contacts/imports/abc/data.xlsx"),
            CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldReturnZeroCounts()
    {
        // Arrange
        _fileStorageService.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new StartContactImportHandler(
            _fileStorageService, _tenantAccessor, _storageOptions,
            _backgroundJobClient, NullLogger<StartContactImportHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new StartContactImportCommand("contacts.csv", "csv", $"{_orgId}/contacts/imports/abc/contacts.csv"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalRows.Should().Be(0);
        result.Value.ProcessedRows.Should().Be(0);
        result.Value.SuccessCount.Should().Be(0);
        result.Value.ErrorCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_StorageKeyWrongOrg_ShouldReturnFailure()
    {
        // Arrange
        var handler = new StartContactImportHandler(
            _fileStorageService, _tenantAccessor, _storageOptions,
            _backgroundJobClient, NullLogger<StartContactImportHandler>.Instance);
        var wrongOrgId = Guid.NewGuid();

        // Act
        var result = await handler.Handle(
            new StartContactImportCommand("contacts.csv", "csv", $"{wrongOrgId}/contacts/imports/abc/contacts.csv"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_import_invalid_storage_key");
    }

    private static ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
