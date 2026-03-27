using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Reporting.Application.Queries;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class GetReportDefinitionsQueryTests : IDisposable
{
    private readonly ReportingDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetReportDefinitionsQueryTests()
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
        var handler = new GetReportDefinitionsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(new GetReportDefinitionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithDefinitions_ReturnsAll()
    {
        await SeedDefinitionsAsync("Revenue Report", "finance", "financial");
        await SeedDefinitionsAsync("User Activity", "analytics", "usage");
        var handler = new GetReportDefinitionsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(new GetReportDefinitionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithModuleFilter_ReturnsMatchingModule()
    {
        await SeedDefinitionsAsync("Revenue", "finance", "financial");
        await SeedDefinitionsAsync("User Activity", "analytics", "usage");
        var handler = new GetReportDefinitionsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(
            new GetReportDefinitionsQuery(Module: "finance"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items[0].Name.Should().Be("Revenue");
    }

    [Fact]
    public async Task Handle_WithCategoryFilter_ReturnsMatchingCategory()
    {
        await SeedDefinitionsAsync("Revenue", "finance", "financial");
        await SeedDefinitionsAsync("User Activity", "analytics", "usage");
        var handler = new GetReportDefinitionsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(
            new GetReportDefinitionsQuery(Category: "usage"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items[0].Name.Should().Be("User Activity");
    }

    [Fact]
    public async Task Handle_WithSearchFilter_FiltersByNameOrDescription()
    {
        await SeedDefinitionsAsync("Revenue Report", "finance", null);
        await SeedDefinitionsAsync("User Activity", "analytics", null);
        var handler = new GetReportDefinitionsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(
            new GetReportDefinitionsQuery(Search: "Revenue"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items[0].Name.Should().Be("Revenue Report");
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        for (var i = 0; i < 5; i++)
            await SeedDefinitionsAsync($"Report {i}", "mod", null);
        var handler = new GetReportDefinitionsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(
            new GetReportDefinitionsQuery(Page: 1, PageSize: 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(5);
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Select(i => i.Name).Should().BeSubsetOf(
            Enumerable.Range(0, 5).Select(i => $"Report {i}"));
    }

    [Fact]
    public async Task Handle_DifferentTenant_DoesNotReturnOtherTenantDefinitions()
    {
        var otherTenantDef = ReportDefinition.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Other Tenant Report", null,
            "mod", null, "SELECT 1", null, ReportFormat.Csv);
        await _dbContext.ReportDefinitions.AddAsync(otherTenantDef);

        await SeedDefinitionsAsync("My Report", "mod", null);
        var handler = new GetReportDefinitionsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(new GetReportDefinitionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items[0].Name.Should().Be("My Report");
    }

    [Fact]
    public async Task Handle_DifferentOrg_DoesNotReturnOtherOrgDefinitions()
    {
        // Seed a definition belonging to a different org within the same tenant
        var otherOrgDef = ReportDefinition.Create(
            _tenantId, Guid.NewGuid(), "Other Org Report", null,
            "mod", null, "SELECT 1", null, ReportFormat.Csv);
        await _dbContext.ReportDefinitions.AddAsync(otherOrgDef);

        await SeedDefinitionsAsync("My Org Report", "mod", null);
        var handler = new GetReportDefinitionsHandler(_dbContext, _tenantAccessor);

        var result = await handler.Handle(new GetReportDefinitionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items[0].Name.Should().Be("My Org Report");
    }

    private async Task SeedDefinitionsAsync(string name, string module, string? category)
    {
        var definition = ReportDefinition.Create(
            _tenantId, _orgId, name, null, module, category,
            "SELECT 1", null, ReportFormat.Csv);
        await _dbContext.ReportDefinitions.AddAsync(definition);
        await _dbContext.SaveChangesAsync();
    }

    public void Dispose() => _dbContext.Dispose();
}
