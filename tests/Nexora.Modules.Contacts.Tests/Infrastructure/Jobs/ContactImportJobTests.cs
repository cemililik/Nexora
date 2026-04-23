using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.Services;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Contacts.Infrastructure.Jobs;
using Nexora.Modules.Contacts.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using NSubstitute;

namespace Nexora.Modules.Contacts.Tests.Infrastructure.Jobs;

/// <summary>Unit tests for <see cref="ContactImportJob"/>.</summary>
public sealed class ContactImportJobTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IFileStorageService _fileStorageService;
    private readonly IContactDuplicateMatcher _duplicateMatcher;
    private readonly IOutbox _outbox;
    private readonly IOptions<StorageOptions> _storageOptions;

    public ContactImportJobTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
        _fileStorageService = Substitute.For<IFileStorageService>();
        _duplicateMatcher = Substitute.For<IContactDuplicateMatcher>();
        _outbox = Substitute.For<IOutbox>();
        _storageOptions = Options.Create(new StorageOptions());
    }

    private ContactImportJob CreateJob() =>
        new(_tenantAccessor, _dbContext, _fileStorageService, _duplicateMatcher,
            _outbox, _storageOptions, NullLogger<ContactImportJob>.Instance);

    private async Task<ImportJob> SeedImportJob(string? mappingJson = null)
    {
        var importJob = ImportJob.Create(
            _tenantId, _orgId, "c.csv", "csv",
            $"{_orgId}/contacts/imports/abc/c.csv", "tester");
        importJob.SetHangfireJobId("hf-1");
        importJob.SetColumnMapping(mappingJson);
        _dbContext.ImportJobs.Add(importJob);
        await _dbContext.SaveChangesAsync();
        return importJob;
    }

    private ContactImportJobParams BuildParams(ImportJob job) => new()
    {
        TenantId = _tenantId.ToString(),
        OrganizationId = _orgId.ToString(),
        OrganizationIdGuid = _orgId,
        FileName = job.FileName,
        FileFormat = job.FileFormat,
        StorageKey = job.StorageKey,
        ImportJobId = job.Id.Value,
    };

    [Fact]
    public async Task ExecuteAsync_WithMapping_ImportsContactsUsingMappedColumns()
    {
        var csv = "Address,E-Mail\nAda,ada@example.com\nGrace,grace@example.com\n";
        _fileStorageService.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(csv));

        var mapping = new Dictionary<string, string>
        {
            ["Address"] = "FirstName",
            ["E-Mail"] = "Email",
        };
        var importJob = await SeedImportJob(JsonSerializer.Serialize(mapping));

        _duplicateMatcher.FindExistingContactIdAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((Guid?)null);

        await CreateJob().RunAsync(BuildParams(importJob), CancellationToken.None);

        var contacts = await _dbContext.Contacts.ToListAsync();
        contacts.Should().HaveCount(2);
        contacts.Select(c => c.Email).Should().BeEquivalentTo(new[] { "ada@example.com", "grace@example.com" });
        contacts.Select(c => c.FirstName).Should().BeEquivalentTo(new[] { "Ada", "Grace" });

        var updated = await _dbContext.ImportJobs.FirstAsync();
        updated.Status.Should().Be(ImportJobStatus.Completed);
        updated.SuccessCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateMatcherMatches_IncrementsErrorCount()
    {
        var csv = "Email\nada@example.com\n";
        _fileStorageService.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(csv));

        var importJob = await SeedImportJob();

        _duplicateMatcher.FindExistingContactIdAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        await CreateJob().RunAsync(BuildParams(importJob), CancellationToken.None);

        var updated = await _dbContext.ImportJobs.FirstAsync();
        updated.SuccessCount.Should().Be(0);
        updated.ErrorCount.Should().Be(1);
        updated.Status.Should().Be(ImportJobStatus.Completed);
        (await _dbContext.Contacts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedFormat_MarksFailedAndRethrows()
    {
        _fileStorageService.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([0x00, 0x01, 0x02]);

        var importJob = ImportJob.Create(
            _tenantId, _orgId, "c.bin", "unknown",
            $"{_orgId}/contacts/imports/abc/c.bin", "tester");
        importJob.SetHangfireJobId("hf-2");
        _dbContext.ImportJobs.Add(importJob);
        await _dbContext.SaveChangesAsync();

        var act = async () => await CreateJob().RunAsync(BuildParams(importJob), CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
        var updated = await _dbContext.ImportJobs.FirstAsync();
        updated.Status.Should().Be(ImportJobStatus.Failed);
    }

    [Fact]
    public void ApplyMapping_NullMapping_UsesIdentityHeaders()
    {
        var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Email"] = "ada@example.com",
            ["FirstName"] = "Ada",
        };

        var result = ContactImportJob.ApplyMapping(row, null);

        result.Email.Should().Be("ada@example.com");
        result.FirstName.Should().Be("Ada");
    }

    public void Dispose() => _dbContext.Dispose();
}
