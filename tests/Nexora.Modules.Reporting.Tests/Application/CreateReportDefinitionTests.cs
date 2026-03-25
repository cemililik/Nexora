using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Reporting.Application.Commands;
using Nexora.Modules.Reporting.Application.Services;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Modules.Reporting.Infrastructure.Services;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class CreateReportDefinitionTests : IDisposable
{
    private readonly ReportingDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly ISqlQueryValidator _sqlQueryValidator = new SqlQueryValidator();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public CreateReportDefinitionTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        _tenantAccessor.SetTenant(_tenantId.ToString(), _orgId.ToString());

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ReportingDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task CreateReportDefinition_WithValidData_ReturnsCreatedDefinition()
    {
        var handler = new CreateReportDefinitionHandler(
            _dbContext, _sqlQueryValidator, _tenantAccessor,
            NullLogger<CreateReportDefinitionHandler>.Instance);

        var command = new CreateReportDefinitionCommand(
            "Revenue Report", "Monthly revenue", "finance", "financial",
            "SELECT SUM(amount) FROM orders", null, "Csv");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Revenue Report");
        result.Value.Module.Should().Be("finance");
        result.Value.DefaultFormat.Should().Be("Csv");
        result.Value.IsActive.Should().BeTrue();

        var saved = await _dbContext.ReportDefinitions.FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Revenue Report");
    }

    [Fact]
    public async Task CreateReportDefinition_WithInvalidFormat_ReturnsFailure()
    {
        var handler = new CreateReportDefinitionHandler(
            _dbContext, _sqlQueryValidator, _tenantAccessor,
            NullLogger<CreateReportDefinitionHandler>.Instance);

        var command = new CreateReportDefinitionCommand(
            "Test", null, "mod", null, "SELECT 1", null, "InvalidFormat");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_reporting_error_invalid_format");
    }

    public void Dispose() => _dbContext.Dispose();
}
