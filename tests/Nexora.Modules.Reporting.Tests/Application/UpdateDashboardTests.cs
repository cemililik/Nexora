using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Reporting.Application.Commands;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class UpdateDashboardTests : IDisposable
{
    private readonly ReportingDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public UpdateDashboardTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        _tenantAccessor.SetTenant(_tenantId.ToString(), _orgId.ToString());

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ReportingDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ExistingDashboard_UpdatesSuccessfully()
    {
        var dashboard = Dashboard.Create(_tenantId, _orgId, "Old Name", null);
        await _dbContext.Dashboards.AddAsync(dashboard);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateDashboardHandler(
            _dbContext, _tenantAccessor,
            NullLogger<UpdateDashboardHandler>.Instance);

        var command = new UpdateDashboardCommand(
            dashboard.Id.Value, "New Name", "New Description",
            "[{\"id\":\"w1\"}]", true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("New Name");
        result.Value.Description.Should().Be("New Description");
        result.Value.Widgets.Should().Contain("w1");
        result.Value.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NonExistentDashboard_ReturnsFailure()
    {
        var handler = new UpdateDashboardHandler(
            _dbContext, _tenantAccessor,
            NullLogger<UpdateDashboardHandler>.Instance);

        var command = new UpdateDashboardCommand(
            Guid.NewGuid(), "Name", null, null, false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_reporting_error_dashboard_not_found");
    }

    [Fact]
    public async Task Handle_DifferentTenantDashboard_ReturnsFailure()
    {
        var otherDashboard = Dashboard.Create(Guid.NewGuid(), Guid.NewGuid(), "Other", null);
        await _dbContext.Dashboards.AddAsync(otherDashboard);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateDashboardHandler(
            _dbContext, _tenantAccessor,
            NullLogger<UpdateDashboardHandler>.Instance);

        var command = new UpdateDashboardCommand(
            otherDashboard.Id.Value, "Updated", null, null, false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UpdatePersistsChanges_ToDatabase()
    {
        var dashboard = Dashboard.Create(_tenantId, _orgId, "Original", null);
        await _dbContext.Dashboards.AddAsync(dashboard);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateDashboardHandler(
            _dbContext, _tenantAccessor,
            NullLogger<UpdateDashboardHandler>.Instance);

        await handler.Handle(
            new UpdateDashboardCommand(dashboard.Id.Value, "Updated", "Desc", null, true),
            CancellationToken.None);

        var saved = await _dbContext.Dashboards.FirstAsync(d => d.Id == dashboard.Id);
        saved.Name.Should().Be("Updated");
        saved.Description.Should().Be("Desc");
        saved.IsDefault.Should().BeTrue();
    }

    public void Dispose() => _dbContext.Dispose();
}
