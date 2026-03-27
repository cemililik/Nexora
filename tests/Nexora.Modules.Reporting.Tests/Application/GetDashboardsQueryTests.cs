using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Reporting.Application.Queries;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class GetDashboardsQueryTests : IDisposable
{
    private readonly ReportingDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetDashboardsQueryTests()
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
        var handler = new GetDashboardsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(new GetDashboardsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithDashboards_ReturnsAll()
    {
        await SeedDashboardAsync("Executive Dashboard", true);
        await SeedDashboardAsync("Sales Dashboard", false);
        var handler = new GetDashboardsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(new GetDashboardsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        for (var i = 0; i < 5; i++)
            await SeedDashboardAsync($"Dashboard {i}");
        var handler = new GetDashboardsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(
            new GetDashboardsQuery(Page: 1, PageSize: 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(5);
        result.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_DifferentTenant_DoesNotReturnOtherTenantDashboards()
    {
        // Vary only tenant ID to isolate the dimension under test (tenant isolation).
        // Keep the same org ID to ensure the filter is on tenant, not org.
        var otherDashboard = Dashboard.Create(
            Guid.NewGuid(), _orgId, "Other Tenant Dashboard", null);
        await _dbContext.Dashboards.AddAsync(otherDashboard);

        await SeedDashboardAsync("My Dashboard");
        var handler = new GetDashboardsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(new GetDashboardsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items[0].Name.Should().Be("My Dashboard");
    }

    [Fact]
    public async Task Handle_ReturnsMappedDto_WithCorrectProperties()
    {
        var dashboard = Dashboard.Create(
            _tenantId, _orgId, "KPI Dashboard", "Key metrics overview", true);
        await _dbContext.Dashboards.AddAsync(dashboard);
        await _dbContext.SaveChangesAsync();
        var handler = new GetDashboardsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(new GetDashboardsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var item = result.Value!.Items[0];
        item.Name.Should().Be("KPI Dashboard");
        item.Description.Should().Be("Key metrics overview");
        item.IsDefault.Should().BeTrue();
    }

    private async Task SeedDashboardAsync(string name, bool isDefault = false)
    {
        var dashboard = Dashboard.Create(_tenantId, _orgId, name, null, isDefault);
        await _dbContext.Dashboards.AddAsync(dashboard);
        await _dbContext.SaveChangesAsync();
    }

    public void Dispose() => _dbContext.Dispose();
}
