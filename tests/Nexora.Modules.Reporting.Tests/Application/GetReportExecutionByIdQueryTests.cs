using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Reporting.Application.Queries;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class GetReportExecutionByIdQueryTests : IDisposable
{
    private readonly ReportingDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetReportExecutionByIdQueryTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        _tenantAccessor.SetTenant(_tenantId.ToString(), _orgId.ToString());

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ReportingDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ExistingExecution_ReturnsExecution()
    {
        var defId = ReportDefinitionId.New();
        var execution = ReportExecution.Create(
            _tenantId, defId, ReportFormat.Csv, null, "user@test.com");
        await _dbContext.ReportExecutions.AddAsync(execution);
        await _dbContext.SaveChangesAsync();

        var handler = new GetReportExecutionByIdHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetReportExecutionByIdHandler>.Instance);

        var result = await handler.Handle(
            new GetReportExecutionByIdQuery(execution.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Queued");
        result.Value.Format.Should().Be("Csv");
        result.Value.ExecutedBy.Should().Be("user@test.com");
    }

    [Fact]
    public async Task Handle_NonExistentExecution_ReturnsFailure()
    {
        var handler = new GetReportExecutionByIdHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetReportExecutionByIdHandler>.Instance);

        var result = await handler.Handle(
            new GetReportExecutionByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_reporting_error_execution_not_found");
    }

    [Fact]
    public async Task Handle_DifferentTenantExecution_ReturnsFailure()
    {
        var otherExecution = ReportExecution.Create(
            Guid.NewGuid(), ReportDefinitionId.New(), ReportFormat.Csv, null, null);
        await _dbContext.ReportExecutions.AddAsync(otherExecution);
        await _dbContext.SaveChangesAsync();

        var handler = new GetReportExecutionByIdHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetReportExecutionByIdHandler>.Instance);

        var result = await handler.Handle(
            new GetReportExecutionByIdQuery(otherExecution.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CompletedExecution_ReturnsMappedDto()
    {
        var defId = ReportDefinitionId.New();
        var execution = ReportExecution.Create(
            _tenantId, defId, ReportFormat.Excel, "{\"id\":\"1\"}", "admin@test.com");
        execution.MarkRunning();
        execution.MarkCompleted("reports/output.xlsx", 42, 1500);
        await _dbContext.ReportExecutions.AddAsync(execution);
        await _dbContext.SaveChangesAsync();

        var handler = new GetReportExecutionByIdHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetReportExecutionByIdHandler>.Instance);

        var result = await handler.Handle(
            new GetReportExecutionByIdQuery(execution.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Completed");
        result.Value.RowCount.Should().Be(42);
        result.Value.DurationMs.Should().Be(1500);
        result.Value.Format.Should().Be("Excel");
        result.Value.ParameterValues.Should().Be("{\"id\":\"1\"}");
        result.Value.ExecutedBy.Should().Be("admin@test.com");
    }

    [Fact]
    public async Task Handle_FailedExecution_ReturnsErrorDetails()
    {
        var defId = ReportDefinitionId.New();
        var execution = ReportExecution.Create(
            _tenantId, defId, ReportFormat.Csv, null, null);
        execution.MarkRunning();
        execution.MarkFailed("Timeout exceeded", 30000);
        await _dbContext.ReportExecutions.AddAsync(execution);
        await _dbContext.SaveChangesAsync();

        var handler = new GetReportExecutionByIdHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetReportExecutionByIdHandler>.Instance);

        var result = await handler.Handle(
            new GetReportExecutionByIdQuery(execution.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Failed");
        result.Value.ErrorDetails.Should().Be("Timeout exceeded");
        result.Value.DurationMs.Should().Be(30000);
    }

    public void Dispose() => _dbContext.Dispose();
}
