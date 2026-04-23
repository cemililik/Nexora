using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Contacts.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class StartContactExportTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IBackgroundJobClient _backgroundJobClient = Substitute.For<IBackgroundJobClient>();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public StartContactExportTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);

        _backgroundJobClient
            .Create(Arg.Any<Job>(), Arg.Any<IState>())
            .Returns("hangfire-job-1");
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldReturnQueuedJob()
    {
        var handler = new StartContactExportHandler(
            _tenantAccessor, _backgroundJobClient, _dbContext,
            NullLogger<StartContactExportHandler>.Instance);

        var result = await handler.Handle(
            new StartContactExportCommand("csv"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Queued");
        result.Value.Format.Should().Be("csv");
        result.Message!.Key.Should().Be("lockey_contacts_export_job_started");
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldEnqueueHangfireJob()
    {
        var handler = new StartContactExportHandler(
            _tenantAccessor, _backgroundJobClient, _dbContext,
            NullLogger<StartContactExportHandler>.Instance);

        await handler.Handle(
            new StartContactExportCommand("xlsx"), CancellationToken.None);

        _backgroundJobClient.Received(1).Create(Arg.Any<Job>(), Arg.Any<EnqueuedState>());
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldPersistExportJobRecord()
    {
        var handler = new StartContactExportHandler(
            _tenantAccessor, _backgroundJobClient, _dbContext,
            NullLogger<StartContactExportHandler>.Instance);

        var result = await handler.Handle(
            new StartContactExportCommand(
                "vcard",
                Fields: new[] { "firstName", "lastName" },
                DateFrom: DateTimeOffset.UtcNow.AddDays(-7),
                DateTo: DateTimeOffset.UtcNow,
                DateField: "CreatedAt"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persisted = await _dbContext.ExportJobs.FirstOrDefaultAsync(
            j => j.Id.Value == result.Value!.JobId);
        persisted.Should().NotBeNull();
        persisted!.Format.Should().Be("vcard");
        persisted.HangfireJobId.Should().Be("hangfire-job-1");
        persisted.FiltersJson.Should().NotBeNull();
        persisted.FieldsJson.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_FormatUpperCase_ShouldNormalizeToLowerCase()
    {
        var handler = new StartContactExportHandler(
            _tenantAccessor, _backgroundJobClient, _dbContext,
            NullLogger<StartContactExportHandler>.Instance);

        var result = await handler.Handle(
            new StartContactExportCommand("CSV"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Format.Should().Be("csv");
    }

    [Fact]
    public async Task Handle_TwoCommands_ShouldReturnUniqueJobIds()
    {
        var handler = new StartContactExportHandler(
            _tenantAccessor, _backgroundJobClient, _dbContext,
            NullLogger<StartContactExportHandler>.Instance);

        var result1 = await handler.Handle(
            new StartContactExportCommand("csv"), CancellationToken.None);
        var result2 = await handler.Handle(
            new StartContactExportCommand("csv"), CancellationToken.None);

        result1.Value!.JobId.Should().NotBe(result2.Value!.JobId);
    }

    public void Dispose() => _dbContext.Dispose();
}
