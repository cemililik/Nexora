using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Reporting.Application.Queries;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class GetDashboardByIdQueryTests : IDisposable
{
    private readonly ReportingDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetDashboardByIdQueryTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        _tenantAccessor.SetTenant(_tenantId.ToString(), _orgId.ToString());

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ReportingDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ExistingDashboard_ReturnsDashboard()
    {
        var dashboard = Dashboard.Create(
            _tenantId, _orgId, "Executive Dashboard", "Overview of KPIs", true);
        await _dbContext.Dashboards.AddAsync(dashboard);
        await _dbContext.SaveChangesAsync();

        var handler = new GetDashboardByIdHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetDashboardByIdHandler>.Instance);

        var result = await handler.Handle(
            new GetDashboardByIdQuery(dashboard.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Executive Dashboard");
        result.Value.Description.Should().Be("Overview of KPIs");
        result.Value.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NonExistentDashboard_ReturnsFailure()
    {
        var handler = new GetDashboardByIdHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetDashboardByIdHandler>.Instance);

        var result = await handler.Handle(
            new GetDashboardByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_reporting_error_dashboard_not_found");
    }

    [Fact]
    public async Task Handle_DifferentTenantDashboard_ReturnsFailure()
    {
        var otherDashboard = Dashboard.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Other Dashboard", null);
        await _dbContext.Dashboards.AddAsync(otherDashboard);
        await _dbContext.SaveChangesAsync();

        var handler = new GetDashboardByIdHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetDashboardByIdHandler>.Instance);

        var result = await handler.Handle(
            new GetDashboardByIdQuery(otherDashboard.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DashboardWithWidgets_ReturnsWidgetsJson()
    {
        var dashboard = Dashboard.Create(_tenantId, _orgId, "Widget Dashboard", null);
        dashboard.Update("Widget Dashboard", null, "[{\"id\":\"w1\",\"type\":\"chart\"}]", false);
        await _dbContext.Dashboards.AddAsync(dashboard);
        await _dbContext.SaveChangesAsync();

        var handler = new GetDashboardByIdHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetDashboardByIdHandler>.Instance);

        var result = await handler.Handle(
            new GetDashboardByIdQuery(dashboard.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Widgets.Should().Contain("w1");
    }

    public void Dispose() => _dbContext.Dispose();
}
