using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Reporting.Application.Commands;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class CreateReportScheduleTests : IDisposable
{
    private readonly ReportingDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public CreateReportScheduleTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        _tenantAccessor.SetTenant(_tenantId.ToString(), _orgId.ToString());

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ReportingDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidData_ReturnsCreatedSchedule()
    {
        var definition = await SeedDefinitionAsync();

        var handler = new CreateReportScheduleHandler(
            _dbContext, _tenantAccessor,
            NullLogger<CreateReportScheduleHandler>.Instance);

        var command = new CreateReportScheduleCommand(
            definition.Id.Value, "0 8 * * 1", "Excel", "[\"admin@test.com\"]");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CronExpression.Should().Be("0 8 * * 1");
        result.Value.Format.Should().Be("Excel");
        result.Value.Recipients.Should().Be("[\"admin@test.com\"]");
        result.Value.IsActive.Should().BeTrue();
        result.Value.DefinitionId.Should().Be(definition.Id.Value);
    }

    [Fact]
    public async Task Handle_DefinitionNotFound_ReturnsFailure()
    {
        var handler = new CreateReportScheduleHandler(
            _dbContext, _tenantAccessor,
            NullLogger<CreateReportScheduleHandler>.Instance);

        var command = new CreateReportScheduleCommand(
            Guid.NewGuid(), "0 0 * * *", "Csv", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_reporting_error_definition_not_found");
    }

    [Fact]
    public async Task Handle_InvalidFormat_ReturnsFailure()
    {
        var definition = await SeedDefinitionAsync();

        var handler = new CreateReportScheduleHandler(
            _dbContext, _tenantAccessor,
            NullLogger<CreateReportScheduleHandler>.Instance);

        var command = new CreateReportScheduleCommand(
            definition.Id.Value, "0 0 * * *", "InvalidFormat", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_reporting_error_invalid_format");
    }

    [Fact]
    public async Task Handle_ValidData_PersistsScheduleToDatabase()
    {
        var definition = await SeedDefinitionAsync();

        var handler = new CreateReportScheduleHandler(
            _dbContext, _tenantAccessor,
            NullLogger<CreateReportScheduleHandler>.Instance);

        var command = new CreateReportScheduleCommand(
            definition.Id.Value, "0 8 * * 1", "Csv", null);

        await handler.Handle(command, CancellationToken.None);

        var count = await _dbContext.ReportSchedules.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithNullRecipients_ReturnsCreatedSchedule()
    {
        var definition = await SeedDefinitionAsync();

        var handler = new CreateReportScheduleHandler(
            _dbContext, _tenantAccessor,
            NullLogger<CreateReportScheduleHandler>.Instance);

        var command = new CreateReportScheduleCommand(
            definition.Id.Value, "0 0 * * *", "Pdf", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Recipients.Should().BeNull();
    }

    private async Task<ReportDefinition> SeedDefinitionAsync()
    {
        var definition = ReportDefinition.Create(
            _tenantId, _orgId, "Test Report", null, "mod", null,
            "SELECT 1", null, ReportFormat.Csv);
        await _dbContext.ReportDefinitions.AddAsync(definition);
        await _dbContext.SaveChangesAsync();
        return definition;
    }

    public void Dispose() => _dbContext.Dispose();
}
