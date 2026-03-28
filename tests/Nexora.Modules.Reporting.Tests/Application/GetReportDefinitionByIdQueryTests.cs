using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Reporting.Application.Queries;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class GetReportDefinitionByIdQueryTests : IDisposable
{
    private readonly ReportingDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetReportDefinitionByIdQueryTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        _tenantAccessor.SetTenant(_tenantId.ToString(), _orgId.ToString());

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ReportingDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ExistingDefinition_ReturnsMappedDto()
    {
        var definition = ReportDefinition.Create(
            _tenantId, _orgId, "Revenue Report", "Monthly revenue",
            "finance", "financial", "SELECT SUM(amount) FROM orders",
            "[{\"name\":\"period\"}]", ReportFormat.Csv);
        await _dbContext.ReportDefinitions.AddAsync(definition);
        await _dbContext.SaveChangesAsync();

        var handler = new GetReportDefinitionByIdHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetReportDefinitionByIdHandler>.Instance);

        var result = await handler.Handle(
            new GetReportDefinitionByIdQuery(definition.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(definition.Id.Value);
        result.Value.Name.Should().Be("Revenue Report");
        result.Value.Description.Should().Be("Monthly revenue");
        result.Value.Module.Should().Be("finance");
        result.Value.Category.Should().Be("financial");
        result.Value.QueryText.Should().Be("SELECT SUM(amount) FROM orders");
        result.Value.Parameters.Should().Be("[{\"name\":\"period\"}]");
        result.Value.DefaultFormat.Should().Be("Csv");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NonExistentDefinition_ReturnsFailure()
    {
        var handler = new GetReportDefinitionByIdHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetReportDefinitionByIdHandler>.Instance);

        var result = await handler.Handle(
            new GetReportDefinitionByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_reporting_error_definition_not_found");
    }

    [Fact]
    public async Task Handle_DifferentTenantDefinition_ReturnsFailure()
    {
        var otherTenantDef = ReportDefinition.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Other", null,
            "mod", null, "SELECT 1", null, ReportFormat.Csv);
        await _dbContext.ReportDefinitions.AddAsync(otherTenantDef);
        await _dbContext.SaveChangesAsync();

        var handler = new GetReportDefinitionByIdHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetReportDefinitionByIdHandler>.Instance);

        var result = await handler.Handle(
            new GetReportDefinitionByIdQuery(otherTenantDef.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_reporting_error_definition_not_found");
    }

    public void Dispose() => _dbContext.Dispose();
}
