using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Reporting.Application.Queries;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class GetReportExecutionsQueryTests : IDisposable
{
    private readonly ReportingDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetReportExecutionsQueryTests()
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
        var handler = new GetReportExecutionsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(new GetReportExecutionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithExecutions_ReturnsAll()
    {
        var defId = ReportDefinitionId.New();
        await SeedExecutionAsync(defId, ReportFormat.Csv);
        await SeedExecutionAsync(defId, ReportFormat.Excel);
        var handler = new GetReportExecutionsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(new GetReportExecutionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithDefinitionIdFilter_ReturnsMatchingExecutions()
    {
        var defId1 = ReportDefinitionId.New();
        var defId2 = ReportDefinitionId.New();
        await SeedExecutionAsync(defId1, ReportFormat.Csv);
        await SeedExecutionAsync(defId1, ReportFormat.Excel);
        await SeedExecutionAsync(defId2, ReportFormat.Csv);
        var handler = new GetReportExecutionsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(
            new GetReportExecutionsQuery(DefinitionId: defId1.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().AllSatisfy(e => e.DefinitionId.Should().Be(defId1.Value));
    }

    [Fact]
    public async Task Handle_WithStatusFilter_ReturnsMatchingStatus()
    {
        var defId = ReportDefinitionId.New();
        var queuedExecution = ReportExecution.Create(
            _tenantId, defId, ReportFormat.Csv, null, null);
        var completedExecution = ReportExecution.Create(
            _tenantId, defId, ReportFormat.Excel, null, null);
        completedExecution.MarkRunning();
        completedExecution.MarkCompleted("key", 10, 500);

        await _dbContext.ReportExecutions.AddRangeAsync(queuedExecution, completedExecution);
        await _dbContext.SaveChangesAsync();

        var handler = new GetReportExecutionsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(
            new GetReportExecutionsQuery(Status: "Completed"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items[0].Status.Should().Be("Completed");
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        var defId = ReportDefinitionId.New();
        for (var i = 0; i < 5; i++)
            await SeedExecutionAsync(defId, ReportFormat.Csv);
        var handler = new GetReportExecutionsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(
            new GetReportExecutionsQuery(Page: 1, PageSize: 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(5);
        result.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_DifferentTenant_DoesNotReturnOtherTenantExecutions()
    {
        var otherExecution = ReportExecution.Create(
            Guid.NewGuid(), ReportDefinitionId.New(), ReportFormat.Csv, null, null);
        await _dbContext.ReportExecutions.AddAsync(otherExecution);

        var myDefId = ReportDefinitionId.New();
        await SeedExecutionAsync(myDefId, ReportFormat.Csv);
        var handler = new GetReportExecutionsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(new GetReportExecutionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithInvalidStatus_ReturnsAllExecutions()
    {
        var defId = ReportDefinitionId.New();
        await SeedExecutionAsync(defId, ReportFormat.Csv);
        await SeedExecutionAsync(defId, ReportFormat.Excel);
        var handler = new GetReportExecutionsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(
            new GetReportExecutionsQuery(Status: "InvalidStatus"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
    }

    private async Task SeedExecutionAsync(ReportDefinitionId definitionId, ReportFormat format)
    {
        var execution = ReportExecution.Create(
            _tenantId, definitionId, format, null, "user@test.com");
        await _dbContext.ReportExecutions.AddAsync(execution);
        await _dbContext.SaveChangesAsync();
    }

    public void Dispose() => _dbContext.Dispose();
}
