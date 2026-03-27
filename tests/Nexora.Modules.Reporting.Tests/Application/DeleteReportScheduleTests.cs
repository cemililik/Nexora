using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Reporting.Application.Commands;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class DeleteReportScheduleTests : IDisposable
{
    private readonly ReportingDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public DeleteReportScheduleTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        _tenantAccessor.SetTenant(_tenantId.ToString(), _orgId.ToString());

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ReportingDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ExistingSchedule_DeletesSuccessfully()
    {
        var schedule = ReportSchedule.Create(
            _tenantId, ReportDefinitionId.New(), "0 8 * * 1",
            ReportFormat.Csv, null);
        await _dbContext.ReportSchedules.AddAsync(schedule);
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteReportScheduleHandler(
            _dbContext, _tenantAccessor,
            NullLogger<DeleteReportScheduleHandler>.Instance);

        var result = await handler.Handle(
            new DeleteReportScheduleCommand(schedule.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NonExistentSchedule_ReturnsFailure()
    {
        var handler = new DeleteReportScheduleHandler(
            _dbContext, _tenantAccessor,
            NullLogger<DeleteReportScheduleHandler>.Instance);

        var result = await handler.Handle(
            new DeleteReportScheduleCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_reporting_error_schedule_not_found");
    }

    [Fact]
    public async Task Handle_DifferentTenantSchedule_ReturnsFailure()
    {
        var otherSchedule = ReportSchedule.Create(
            Guid.NewGuid(), ReportDefinitionId.New(), "0 0 * * *",
            ReportFormat.Csv, null);
        await _dbContext.ReportSchedules.AddAsync(otherSchedule);
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteReportScheduleHandler(
            _dbContext, _tenantAccessor,
            NullLogger<DeleteReportScheduleHandler>.Instance);

        var result = await handler.Handle(
            new DeleteReportScheduleCommand(otherSchedule.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    public void Dispose() => _dbContext.Dispose();
}
