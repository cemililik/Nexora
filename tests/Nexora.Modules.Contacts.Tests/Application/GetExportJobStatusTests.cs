using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Contacts.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class GetExportJobStatusTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IFileStorageService _fileStorageService = Substitute.For<IFileStorageService>();
    private readonly IOptions<StorageOptions> _storageOptions =
        Options.Create(new StorageOptions { BucketPrefix = "nexora" });
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetExportJobStatusTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    private GetExportJobStatusHandler CreateHandler() => new(
        _dbContext, _fileStorageService, _storageOptions, _tenantAccessor,
        NullLogger<GetExportJobStatusHandler>.Instance);

    [Fact]
    public async Task Handle_NonExistentJob_ShouldReturnNotFound()
    {
        var result = await CreateHandler().Handle(
            new GetExportJobStatusQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_contacts_export_job_not_found");
    }

    [Fact]
    public async Task Handle_InProgressJob_ShouldReturnStatusWithoutUrl()
    {
        var job = ExportJob.Create(_tenantId, _orgId, "csv", null, null, "user-1");
        job.SetHangfireJobId("h-1");
        await _dbContext.ExportJobs.AddAsync(job);
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(
            new GetExportJobStatusQuery(job.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Queued");
        result.Value.DownloadUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CompletedJob_ShouldReturnPresignedDownloadUrl()
    {
        var job = ExportJob.Create(_tenantId, _orgId, "csv", null, null, "user-1");
        job.SetHangfireJobId("h-2");
        job.MarkProcessing(42);
        var storageKey = $"{_orgId}/contacts/exports/{job.Id.Value}.csv";
        job.MarkCompleted(storageKey);
        await _dbContext.ExportJobs.AddAsync(job);
        await _dbContext.SaveChangesAsync();

        _fileStorageService
            .GenerateDownloadPresignedUrlAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new PresignedUrlResult("https://signed.example/download", DateTimeOffset.UtcNow.AddMinutes(15)));

        var result = await CreateHandler().Handle(
            new GetExportJobStatusQuery(job.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Completed");
        result.Value.TotalRows.Should().Be(42);
        result.Value.DownloadUrl.Should().Be("https://signed.example/download");
    }

    public void Dispose() => _dbContext.Dispose();
}
