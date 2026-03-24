using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Reporting.Application.Commands;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class ExecuteReportTests : IDisposable
{
    private readonly ReportingDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public ExecuteReportTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        _tenantAccessor.SetTenant(_tenantId.ToString(), _orgId.ToString());

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ReportingDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidDefinition_ShouldCreateQueuedExecution()
    {
        var definition = ReportDefinition.Create(
            _tenantId, _orgId, "Test Report", null, "mod", null,
            "SELECT 1", null, ReportFormat.Csv);
        await _dbContext.ReportDefinitions.AddAsync(definition);
        await _dbContext.SaveChangesAsync();

        var handler = new ExecuteReportHandler(
            _dbContext, _tenantAccessor,
            NullLogger<ExecuteReportHandler>.Instance);

        var command = new ExecuteReportCommand(definition.Id.Value, null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Queued");
        result.Value.DefinitionId.Should().Be(definition.Id.Value);
        result.Value.Format.Should().Be("Csv");

        var execution = await _dbContext.ReportExecutions.FirstOrDefaultAsync();
        execution.Should().NotBeNull();
        execution!.Status.Should().Be(ReportStatus.Queued);
    }

    [Fact]
    public async Task Handle_WithFormatOverride_ShouldUseProvidedFormat()
    {
        var definition = ReportDefinition.Create(
            _tenantId, _orgId, "Test", null, "mod", null,
            "SELECT 1", null, ReportFormat.Csv);
        await _dbContext.ReportDefinitions.AddAsync(definition);
        await _dbContext.SaveChangesAsync();

        var handler = new ExecuteReportHandler(
            _dbContext, _tenantAccessor,
            NullLogger<ExecuteReportHandler>.Instance);

        var command = new ExecuteReportCommand(definition.Id.Value, "Excel", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Format.Should().Be("Excel");
    }

    [Fact]
    public async Task Handle_DefinitionNotFound_ShouldReturnFailure()
    {
        var handler = new ExecuteReportHandler(
            _dbContext, _tenantAccessor,
            NullLogger<ExecuteReportHandler>.Instance);

        var command = new ExecuteReportCommand(Guid.NewGuid(), null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_reporting_error_definition_not_found");
    }

    [Fact]
    public async Task Handle_InactiveDefinition_ShouldReturnFailure()
    {
        var definition = ReportDefinition.Create(
            _tenantId, _orgId, "Test", null, "mod", null,
            "SELECT 1", null, ReportFormat.Csv);
        definition.Deactivate();
        await _dbContext.ReportDefinitions.AddAsync(definition);
        await _dbContext.SaveChangesAsync();

        var handler = new ExecuteReportHandler(
            _dbContext, _tenantAccessor,
            NullLogger<ExecuteReportHandler>.Instance);

        var command = new ExecuteReportCommand(definition.Id.Value, null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    public void Dispose() => _dbContext.Dispose();
}
