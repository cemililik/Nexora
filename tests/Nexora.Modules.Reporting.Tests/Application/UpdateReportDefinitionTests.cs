using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Reporting.Application.Commands;
using Nexora.Modules.Reporting.Application.Services;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Modules.Reporting.Infrastructure.Services;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class UpdateReportDefinitionTests : IDisposable
{
    private readonly ReportingDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly ISqlQueryValidator _sqlQueryValidator = new SqlQueryValidator();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public UpdateReportDefinitionTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        _tenantAccessor.SetTenant(_tenantId.ToString(), _orgId.ToString());

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ReportingDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ExistingDefinition_UpdatesSuccessfully()
    {
        var definition = await SeedDefinitionAsync();

        var handler = new UpdateReportDefinitionHandler(
            _dbContext, _sqlQueryValidator, _tenantAccessor,
            NullLogger<UpdateReportDefinitionHandler>.Instance);

        var command = new UpdateReportDefinitionCommand(
            definition.Id.Value, "Updated Name", "New Desc", "analytics",
            "usage", "SELECT COUNT(*) FROM users", null, "Excel");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Updated Name");
        result.Value.Description.Should().Be("New Desc");
        result.Value.Module.Should().Be("analytics");
        result.Value.DefaultFormat.Should().Be("Excel");
    }

    [Fact]
    public async Task Handle_NonExistentDefinition_ReturnsFailure()
    {
        var handler = new UpdateReportDefinitionHandler(
            _dbContext, _sqlQueryValidator, _tenantAccessor,
            NullLogger<UpdateReportDefinitionHandler>.Instance);

        var command = new UpdateReportDefinitionCommand(
            Guid.NewGuid(), "Name", null, "mod", null, "SELECT 1", null, "Csv");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_reporting_error_definition_not_found");
    }

    [Fact]
    public async Task Handle_InvalidSqlQuery_ReturnsFailure()
    {
        var definition = await SeedDefinitionAsync();

        var handler = new UpdateReportDefinitionHandler(
            _dbContext, _sqlQueryValidator, _tenantAccessor,
            NullLogger<UpdateReportDefinitionHandler>.Instance);

        var command = new UpdateReportDefinitionCommand(
            definition.Id.Value, "Name", null, "mod", null,
            "DROP TABLE users", null, "Csv");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_InvalidFormat_ReturnsFailure()
    {
        var definition = await SeedDefinitionAsync();

        var handler = new UpdateReportDefinitionHandler(
            _dbContext, _sqlQueryValidator, _tenantAccessor,
            NullLogger<UpdateReportDefinitionHandler>.Instance);

        var command = new UpdateReportDefinitionCommand(
            definition.Id.Value, "Name", null, "mod", null,
            "SELECT 1", null, "BadFormat");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_reporting_error_invalid_format");
    }

    [Fact]
    public async Task Handle_DifferentTenantDefinition_ReturnsFailure()
    {
        var otherDef = ReportDefinition.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Other", null,
            "mod", null, "SELECT 1", null, ReportFormat.Csv);
        await _dbContext.ReportDefinitions.AddAsync(otherDef);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateReportDefinitionHandler(
            _dbContext, _sqlQueryValidator, _tenantAccessor,
            NullLogger<UpdateReportDefinitionHandler>.Instance);

        var command = new UpdateReportDefinitionCommand(
            otherDef.Id.Value, "Name", null, "mod", null, "SELECT 1", null, "Csv");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    private async Task<ReportDefinition> SeedDefinitionAsync()
    {
        var definition = ReportDefinition.Create(
            _tenantId, _orgId, "Original Report", null, "finance", null,
            "SELECT 1", null, ReportFormat.Csv);
        await _dbContext.ReportDefinitions.AddAsync(definition);
        await _dbContext.SaveChangesAsync();
        return definition;
    }

    public void Dispose() => _dbContext.Dispose();
}
