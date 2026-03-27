using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Reporting.Application.Commands;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class DeleteDashboardTests : IDisposable
{
    private readonly ReportingDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public DeleteDashboardTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        _tenantAccessor.SetTenant(_tenantId.ToString(), _orgId.ToString());

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ReportingDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ExistingDashboard_DeletesSuccessfully()
    {
        var dashboard = Dashboard.Create(_tenantId, _orgId, "To Delete", null);
        await _dbContext.Dashboards.AddAsync(dashboard);
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteDashboardHandler(
            _dbContext, _tenantAccessor,
            NullLogger<DeleteDashboardHandler>.Instance);

        var result = await handler.Handle(
            new DeleteDashboardCommand(dashboard.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NonExistentDashboard_ReturnsFailure()
    {
        var handler = new DeleteDashboardHandler(
            _dbContext, _tenantAccessor,
            NullLogger<DeleteDashboardHandler>.Instance);

        var result = await handler.Handle(
            new DeleteDashboardCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_reporting_error_dashboard_not_found");
    }

    [Fact]
    public async Task Handle_DifferentTenantDashboard_ReturnsFailure()
    {
        var otherDashboard = Dashboard.Create(Guid.NewGuid(), Guid.NewGuid(), "Other", null);
        await _dbContext.Dashboards.AddAsync(otherDashboard);
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteDashboardHandler(
            _dbContext, _tenantAccessor,
            NullLogger<DeleteDashboardHandler>.Instance);

        var result = await handler.Handle(
            new DeleteDashboardCommand(otherDashboard.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    public void Dispose() => _dbContext.Dispose();
}
