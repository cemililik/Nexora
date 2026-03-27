using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Reporting.Application.Commands;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class UpdateReportScheduleTests : IDisposable
{
    private readonly ReportingDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public UpdateReportScheduleTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        _tenantAccessor.SetTenant(_tenantId.ToString(), _orgId.ToString());

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ReportingDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ExistingSchedule_UpdatesSuccessfully()
    {
        var schedule = await SeedScheduleAsync();

        var handler = new UpdateReportScheduleHandler(
            _dbContext, _tenantAccessor,
            NullLogger<UpdateReportScheduleHandler>.Instance);

        var command = new UpdateReportScheduleCommand(
            schedule.Id.Value, "0 12 * * *", "Pdf", "[\"user@test.com\"]");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CronExpression.Should().Be("0 12 * * *");
        result.Value.Format.Should().Be("Pdf");
        result.Value.Recipients.Should().Be("[\"user@test.com\"]");
    }

    [Fact]
    public async Task Handle_NonExistentSchedule_ReturnsFailure()
    {
        var handler = new UpdateReportScheduleHandler(
            _dbContext, _tenantAccessor,
            NullLogger<UpdateReportScheduleHandler>.Instance);

        var command = new UpdateReportScheduleCommand(
            Guid.NewGuid(), "0 0 * * *", "Csv", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_reporting_error_schedule_not_found");
    }

    [Fact]
    public async Task Handle_InvalidFormat_ReturnsFailure()
    {
        var schedule = await SeedScheduleAsync();

        var handler = new UpdateReportScheduleHandler(
            _dbContext, _tenantAccessor,
            NullLogger<UpdateReportScheduleHandler>.Instance);

        var command = new UpdateReportScheduleCommand(
            schedule.Id.Value, "0 0 * * *", "BadFormat", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_reporting_error_invalid_format");
    }

    [Fact]
    public async Task Handle_DifferentTenantSchedule_ReturnsFailure()
    {
        var otherSchedule = ReportSchedule.Create(
            Guid.NewGuid(), ReportDefinitionId.New(), "0 0 * * *",
            ReportFormat.Csv, null);
        await _dbContext.ReportSchedules.AddAsync(otherSchedule);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateReportScheduleHandler(
            _dbContext, _tenantAccessor,
            NullLogger<UpdateReportScheduleHandler>.Instance);

        var command = new UpdateReportScheduleCommand(
            otherSchedule.Id.Value, "0 8 * * 1", "Csv", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    private async Task<ReportSchedule> SeedScheduleAsync()
    {
        var schedule = ReportSchedule.Create(
            _tenantId, ReportDefinitionId.New(), "0 8 * * 1",
            ReportFormat.Csv, null);
        await _dbContext.ReportSchedules.AddAsync(schedule);
        await _dbContext.SaveChangesAsync();
        return schedule;
    }

    public void Dispose() => _dbContext.Dispose();
}
