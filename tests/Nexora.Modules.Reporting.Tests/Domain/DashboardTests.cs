using Nexora.Modules.Reporting.Domain.Entities;

namespace Nexora.Modules.Reporting.Tests.Domain;

public sealed class DashboardTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldSetProperties()
    {
        var dashboard = Dashboard.Create(
            _tenantId, _orgId, "Executive Dashboard", "Overview of KPIs", true);

        dashboard.Id.Value.Should().NotBeEmpty();
        dashboard.TenantId.Should().Be(_tenantId);
        dashboard.OrganizationId.Should().Be(_orgId);
        dashboard.Name.Should().Be("Executive Dashboard");
        dashboard.Description.Should().Be("Overview of KPIs");
        dashboard.IsDefault.Should().BeTrue();
        dashboard.Widgets.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldTrimStrings()
    {
        var dashboard = Dashboard.Create(
            _tenantId, _orgId, "  Dashboard  ", "  Desc  ");

        dashboard.Name.Should().Be("Dashboard");
        dashboard.Description.Should().Be("Desc");
    }

    [Fact]
    public void Create_DefaultIsNotDefault()
    {
        var dashboard = Dashboard.Create(
            _tenantId, _orgId, "Test", null);

        dashboard.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Update_ShouldChangeProperties()
    {
        var dashboard = Dashboard.Create(
            _tenantId, _orgId, "Old Name", null);

        dashboard.Update("New Name", "New Desc", "[{\"id\":\"1\"}]", true);

        dashboard.Name.Should().Be("New Name");
        dashboard.Description.Should().Be("New Desc");
        dashboard.Widgets.Should().Be("[{\"id\":\"1\"}]");
        dashboard.IsDefault.Should().BeTrue();
    }
}
