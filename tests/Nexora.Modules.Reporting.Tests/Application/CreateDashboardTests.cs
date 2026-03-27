using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Reporting.Application.Commands;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class CreateDashboardTests : IDisposable
{
    private readonly ReportingDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public CreateDashboardTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        _tenantAccessor.SetTenant(_tenantId.ToString(), _orgId.ToString());

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ReportingDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidData_ReturnsCreatedDashboard()
    {
        var handler = new CreateDashboardHandler(
            _dbContext, _tenantAccessor,
            NullLogger<CreateDashboardHandler>.Instance);

        var command = new CreateDashboardCommand("Executive Dashboard", "Overview of KPIs", true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Executive Dashboard");
        result.Value.Description.Should().Be("Overview of KPIs");
        result.Value.IsDefault.Should().BeTrue();

        var saved = await _dbContext.Dashboards.FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Executive Dashboard");
    }

    [Fact]
    public async Task Handle_WithoutDescription_ReturnsCreatedDashboard()
    {
        var handler = new CreateDashboardHandler(
            _dbContext, _tenantAccessor,
            NullLogger<CreateDashboardHandler>.Instance);

        var command = new CreateDashboardCommand("Simple Dashboard", null, false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Simple Dashboard");
        result.Value.Description.Should().BeNull();
        result.Value.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidData_PersistsDashboardToDatabase()
    {
        var handler = new CreateDashboardHandler(
            _dbContext, _tenantAccessor,
            NullLogger<CreateDashboardHandler>.Instance);

        var command = new CreateDashboardCommand("Test Dashboard", null, false);

        await handler.Handle(command, CancellationToken.None);

        var count = await _dbContext.Dashboards.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_MultipleDashboards_CreatesEachSuccessfully()
    {
        var handler = new CreateDashboardHandler(
            _dbContext, _tenantAccessor,
            NullLogger<CreateDashboardHandler>.Instance);

        await handler.Handle(new CreateDashboardCommand("Dashboard 1", null, true), CancellationToken.None);
        await handler.Handle(new CreateDashboardCommand("Dashboard 2", null, false), CancellationToken.None);

        var count = await _dbContext.Dashboards.CountAsync();
        count.Should().Be(2);
    }

    public void Dispose() => _dbContext.Dispose();
}
