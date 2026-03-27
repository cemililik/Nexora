using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Reporting.Application.Queries;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class GetReportSchedulesQueryTests : IDisposable
{
    private readonly ReportingDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetReportSchedulesQueryTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        _tenantAccessor.SetTenant(_tenantId.ToString(), _orgId.ToString());

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ReportingDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsEmptyList()
    {
        var handler = new GetReportSchedulesHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(new GetReportSchedulesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithSchedules_ReturnsAll()
    {
        var defId1 = ReportDefinitionId.New();
        var defId2 = ReportDefinitionId.New();
        await SeedScheduleAsync(defId1, "0 8 * * 1");
        await SeedScheduleAsync(defId2, "0 0 * * *");
        var handler = new GetReportSchedulesHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(new GetReportSchedulesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithDefinitionIdFilter_ReturnsMatchingSchedules()
    {
        var defId1 = ReportDefinitionId.New();
        var defId2 = ReportDefinitionId.New();
        await SeedScheduleAsync(defId1, "0 8 * * 1");
        await SeedScheduleAsync(defId1, "0 12 * * *");
        await SeedScheduleAsync(defId2, "0 0 * * *");
        var handler = new GetReportSchedulesHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(
            new GetReportSchedulesQuery(DefinitionId: defId1.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().AllSatisfy(s => s.DefinitionId.Should().Be(defId1.Value));
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        var defId = ReportDefinitionId.New();
        for (var i = 0; i < 5; i++)
            await SeedScheduleAsync(defId, $"0 {i} * * *");
        var handler = new GetReportSchedulesHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(
            new GetReportSchedulesQuery(Page: 1, PageSize: 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(5);
        result.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_DifferentTenant_DoesNotReturnOtherTenantSchedules()
    {
        var otherSchedule = ReportSchedule.Create(
            Guid.NewGuid(), ReportDefinitionId.New(), "0 0 * * *",
            ReportFormat.Csv, null);
        await _dbContext.ReportSchedules.AddAsync(otherSchedule);

        var myDefId = ReportDefinitionId.New();
        await SeedScheduleAsync(myDefId, "0 8 * * 1");
        var handler = new GetReportSchedulesHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(new GetReportSchedulesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ReturnsMappedDto_WithCorrectProperties()
    {
        var defId = ReportDefinitionId.New();
        var schedule = ReportSchedule.Create(
            _tenantId, defId, "0 8 * * 1", ReportFormat.Excel, "[\"admin@test.com\"]");
        await _dbContext.ReportSchedules.AddAsync(schedule);
        await _dbContext.SaveChangesAsync();
        var handler = new GetReportSchedulesHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(new GetReportSchedulesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var item = result.Value!.Items[0];
        item.CronExpression.Should().Be("0 8 * * 1");
        item.Format.Should().Be("Excel");
        item.Recipients.Should().Be("[\"admin@test.com\"]");
        item.IsActive.Should().BeTrue();
    }

    private async Task SeedScheduleAsync(ReportDefinitionId definitionId, string cron)
    {
        var schedule = ReportSchedule.Create(
            _tenantId, definitionId, cron, ReportFormat.Csv, null);
        await _dbContext.ReportSchedules.AddAsync(schedule);
        await _dbContext.SaveChangesAsync();
    }

    public void Dispose() => _dbContext.Dispose();
}
