using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Contacts.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using NSubstitute;

namespace Nexora.Modules.Contacts.Tests.Application;

/// <summary>Unit tests for <see cref="StartContactImportHandler"/>.</summary>
public sealed class StartContactImportTests : IDisposable
{
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IFileStorageService _fileStorageService;
    private readonly IOptions<StorageOptions> _storageOptions;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ContactsDbContext _dbContext;

    public StartContactImportTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        _fileStorageService = Substitute.For<IFileStorageService>();
        _storageOptions = Options.Create(new StorageOptions());
        _backgroundJobClient = Substitute.For<IBackgroundJobClient>();
        _backgroundJobClient.Create(Arg.Any<Hangfire.Common.Job>(), Arg.Any<Hangfire.States.IState>())
            .Returns("hangfire-default");

        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidCommand_FileExists_ReturnsQueuedJob()
    {
        // Arrange
        _fileStorageService.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _backgroundJobClient.Create(Arg.Any<Hangfire.Common.Job>(), Arg.Any<Hangfire.States.IState>())
            .Returns("hangfire-123");
        var handler = new StartContactImportHandler(
            _fileStorageService, _tenantAccessor, _storageOptions,
            _backgroundJobClient, _dbContext, NullLogger<StartContactImportHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new StartContactImportCommand("contacts.csv", "csv", $"{_orgId}/contacts/imports/abc/contacts.csv"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Queued");
        result.Value.JobId.Should().NotBeEmpty();
        _backgroundJobClient.Received(1).Create(Arg.Any<Hangfire.Common.Job>(), Arg.Any<Hangfire.States.IState>());
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsImportJobToDatabase()
    {
        // Arrange
        _fileStorageService.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _backgroundJobClient.Create(Arg.Any<Hangfire.Common.Job>(), Arg.Any<Hangfire.States.IState>())
            .Returns("hangfire-456");
        var handler = new StartContactImportHandler(
            _fileStorageService, _tenantAccessor, _storageOptions,
            _backgroundJobClient, _dbContext, NullLogger<StartContactImportHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new StartContactImportCommand("contacts.csv", "csv", $"{_orgId}/contacts/imports/abc/contacts.csv"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var importJob = await _dbContext.ImportJobs.FirstOrDefaultAsync();
        importJob.Should().NotBeNull();
        importJob!.HangfireJobId.Should().Be("hangfire-456");
        importJob.FileName.Should().Be("contacts.csv");
        importJob.FileFormat.Should().Be("csv");
    }

    [Fact]
    public async Task Handle_FileNotInStorage_ReturnsFailure()
    {
        // Arrange
        _fileStorageService.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var handler = new StartContactImportHandler(
            _fileStorageService, _tenantAccessor, _storageOptions,
            _backgroundJobClient, _dbContext, NullLogger<StartContactImportHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new StartContactImportCommand("contacts.csv", "csv", $"{_orgId}/contacts/imports/abc/contacts.csv"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_import_file_not_found");
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsCorrectTimestamp()
    {
        // Arrange
        _fileStorageService.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new StartContactImportHandler(
            _fileStorageService, _tenantAccessor, _storageOptions,
            _backgroundJobClient, _dbContext, NullLogger<StartContactImportHandler>.Instance);

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
    public async Task Handle_ValidCommand_ReturnsZeroCounts()
    {
        // Arrange
        _fileStorageService.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new StartContactImportHandler(
            _fileStorageService, _tenantAccessor, _storageOptions,
            _backgroundJobClient, _dbContext, NullLogger<StartContactImportHandler>.Instance);

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
    public async Task Handle_StorageKeyWrongOrg_ReturnsFailure()
    {
        // Arrange
        var handler = new StartContactImportHandler(
            _fileStorageService, _tenantAccessor, _storageOptions,
            _backgroundJobClient, _dbContext, NullLogger<StartContactImportHandler>.Instance);
        var wrongOrgId = Guid.NewGuid();

        // Act
        var result = await handler.Handle(
            new StartContactImportCommand("contacts.csv", "csv", $"{wrongOrgId}/contacts/imports/abc/contacts.csv"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_import_invalid_storage_key");
        await _fileStorageService.DidNotReceive()
            .ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _backgroundJobClient.DidNotReceive()
            .Create(Arg.Any<Hangfire.Common.Job>(), Arg.Any<Hangfire.States.IState>());
    }

    public void Dispose() => _dbContext.Dispose();
}
