using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Contacts.Infrastructure.Jobs;
using Nexora.Modules.Contacts.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.Localization;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;
using Nexora.SharedKernel.Abstractions.Storage;

namespace Nexora.Modules.Contacts.Tests.Infrastructure.Jobs;

public sealed class ContactExportJobTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IFileStorageService _storage = Substitute.For<IFileStorageService>();
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();
    private readonly ILocaleContext _locale = Substitute.For<ILocaleContext>();
    private readonly IOptions<StorageOptions> _storageOptions =
        Options.Create(new StorageOptions { BucketPrefix = "nexora" });
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public ContactExportJobTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
        _locale.Locale.Returns("en-US");
    }

    private ContactExportJob CreateJob() => new(
        _tenantAccessor, _dbContext, _storage, _storageOptions, _outbox, _locale,
        NullLogger<ContactExportJob>.Instance);

    private async Task<ExportJob> SeedExportJobAsync(
        string format, Guid? userId = null, string? filtersJson = null, string? fieldsJson = null)
    {
        var job = ExportJob.Create(_tenantId, _orgId, format, filtersJson, fieldsJson,
            userId?.ToString());
        job.SetHangfireJobId("h-1");
        await _dbContext.ExportJobs.AddAsync(job);
        await _dbContext.SaveChangesAsync();
        return job;
    }

    private async Task<Contact> SeedContactAsync(
        string? firstName, string? lastName, string? email = null, string? companyName = null)
    {
        var contact = Contact.Create(
            _tenantId, _orgId, ContactType.Individual,
            firstName, lastName, companyName, email, phone: null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();
        return contact;
    }

    private ContactExportJobParams ParamsFor(ExportJob job, string format,
        IReadOnlyList<string>? fields = null,
        DateTimeOffset? dateFrom = null,
        DateTimeOffset? dateTo = null,
        string? dateField = null,
        Guid? triggeredBy = null) =>
        new()
        {
            TenantId = _tenantId.ToString(),
            OrganizationId = _orgId.ToString(),
            OrganizationIdGuid = _orgId,
            ExportJobId = job.Id.Value,
            Format = format,
            Fields = fields,
            DateFrom = dateFrom,
            DateTo = dateTo,
            DateField = dateField,
            TriggeredByUserId = triggeredBy
        };

    [Fact]
    public async Task Execute_Csv_ShouldUploadAndCompleteJob()
    {
        var triggeredBy = Guid.NewGuid();
        await SeedContactAsync("Alice", "Adams", "alice@example.com");
        await SeedContactAsync("Bob", "Brown", "bob@example.com");
        var job = await SeedExportJobAsync("csv");

        byte[]? uploadedBytes = null;
        string? uploadedKey = null;
        string? uploadedContentType = null;
        await _storage.UploadObjectAsync(
            Arg.Any<string>(), Arg.Do<string>(k => uploadedKey = k),
            Arg.Do<byte[]>(b => uploadedBytes = b), Arg.Do<string>(t => uploadedContentType = t),
            Arg.Any<CancellationToken>());

        await CreateJob().RunAsync(
            ParamsFor(job, "csv", triggeredBy: triggeredBy), CancellationToken.None);

        uploadedBytes.Should().NotBeNull();
        uploadedContentType.Should().Be("text/csv");
        uploadedKey.Should().EndWith(".csv");
        var csvText = Encoding.UTF8.GetString(uploadedBytes!);
        csvText.Should().Contain("alice@example.com");
        csvText.Should().Contain("bob@example.com");

        // T-028: job must emit ContactExportCompletedIntegrationEvent via the
        // outbox (NOT via a direct notification send). Assert both the call
        // shape and the payload so a future refactor that drops fields surfaces
        // immediately.
        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<ContactExportCompletedIntegrationEvent>(e =>
                e.JobId == job.Id.Value &&
                e.TenantId == _tenantId.ToString() &&
                e.Format == "csv" &&
                e.TotalRows == 2 &&
                e.TriggeredByUserId == triggeredBy &&
                e.OrganizationId == _orgId),
            Arg.Any<CancellationToken>());

        var persisted = await _dbContext.ExportJobs.AsNoTracking()
            .FirstAsync(j => j.Id == job.Id);
        persisted.Status.Should().Be(ExportJobStatus.Completed);
        persisted.TotalRows.Should().Be(2);
        persisted.StorageKey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Execute_Xlsx_ShouldUploadSpreadsheet()
    {
        await SeedContactAsync("Alice", "Adams", "alice@example.com");
        var job = await SeedExportJobAsync("xlsx");

        byte[]? uploadedBytes = null;
        string? contentType = null;
        await _storage.UploadObjectAsync(
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Do<byte[]>(b => uploadedBytes = b), Arg.Do<string>(t => contentType = t),
            Arg.Any<CancellationToken>());

        await CreateJob().RunAsync(ParamsFor(job, "xlsx"), CancellationToken.None);

        contentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        uploadedBytes.Should().NotBeNull();
        using var ms = new MemoryStream(uploadedBytes!);
        using var wb = new XLWorkbook(ms);
        wb.Worksheets.Should().ContainSingle();
        wb.Worksheets.First().Name.Should().Be("Contacts");
    }

    [Fact]
    public async Task Execute_VCard_ShouldEmitRfc2426Blocks()
    {
        await SeedContactAsync("Alice", "Adams", "alice@example.com", "ACME, Inc.");
        var job = await SeedExportJobAsync("vcard");

        byte[]? uploadedBytes = null;
        string? contentType = null;
        string? uploadedKey = null;
        await _storage.UploadObjectAsync(
            Arg.Any<string>(), Arg.Do<string>(k => uploadedKey = k),
            Arg.Do<byte[]>(b => uploadedBytes = b), Arg.Do<string>(t => contentType = t),
            Arg.Any<CancellationToken>());

        await CreateJob().RunAsync(ParamsFor(job, "vcard"), CancellationToken.None);

        contentType.Should().Be("text/vcard");
        uploadedKey.Should().EndWith(".vcf");
        var vcard = Encoding.UTF8.GetString(uploadedBytes!);
        vcard.Should().Contain("BEGIN:VCARD");
        vcard.Should().Contain("VERSION:3.0");
        vcard.Should().Contain("FN:");
        vcard.Should().Contain("N:Adams;Alice;;;");
        vcard.Should().Contain("EMAIL;TYPE=INTERNET:alice@example.com");
        vcard.Should().Contain("ORG:ACME\\, Inc.");
        vcard.Should().Contain("END:VCARD");
    }

    [Fact]
    public async Task Execute_WithDateRangeFilter_ShouldExcludeOutOfRangeContacts()
    {
        var old = await SeedContactAsync("Old", "Timer", "old@example.com");
        var recent = await SeedContactAsync("New", "Arrival", "new@example.com");
        _ = old; _ = recent;

        var job = await SeedExportJobAsync("csv");

        byte[]? uploadedBytes = null;
        await _storage.UploadObjectAsync(
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Do<byte[]>(b => uploadedBytes = b), Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        // Both contacts were just created; a distant past date range excludes all of them.
        await CreateJob().RunAsync(
            ParamsFor(job, "csv",
                dateFrom: DateTimeOffset.UtcNow.AddYears(-10),
                dateTo: DateTimeOffset.UtcNow.AddYears(-5),
                dateField: "CreatedAt"),
            CancellationToken.None);

        var persisted = await _dbContext.ExportJobs.AsNoTracking()
            .FirstAsync(j => j.Id == job.Id);
        persisted.TotalRows.Should().Be(0);

        var csv = Encoding.UTF8.GetString(uploadedBytes!);
        csv.Should().NotContain("old@example.com");
        csv.Should().NotContain("new@example.com");
    }

    [Fact]
    public async Task Execute_WithFieldSelection_ShouldOnlyIncludeSelectedColumns()
    {
        await SeedContactAsync("Alice", "Adams", "alice@example.com");
        var job = await SeedExportJobAsync("csv");

        byte[]? uploadedBytes = null;
        await _storage.UploadObjectAsync(
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Do<byte[]>(b => uploadedBytes = b), Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await CreateJob().RunAsync(
            ParamsFor(job, "csv", fields: new[] { "firstName", "lastName" }),
            CancellationToken.None);

        var csv = Encoding.UTF8.GetString(uploadedBytes!).TrimStart('﻿');
        var firstLine = csv.Split('\n')[0].Trim();
        firstLine.Should().Be("firstName,lastName");
        csv.Should().NotContain("alice@example.com");
    }

    [Fact]
    public async Task Execute_AlreadyCompletedJob_ShouldSkipIdempotently()
    {
        var job = await SeedExportJobAsync("csv");
        job.MarkProcessing(0);
        job.MarkCompleted($"{_orgId}/contacts/exports/{job.Id.Value}.csv");
        _dbContext.ExportJobs.Update(job);
        await _dbContext.SaveChangesAsync();

        await CreateJob().RunAsync(ParamsFor(job, "csv"), CancellationToken.None);

        await _storage.DidNotReceive().UploadObjectAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    public void Dispose() => _dbContext.Dispose();
}
