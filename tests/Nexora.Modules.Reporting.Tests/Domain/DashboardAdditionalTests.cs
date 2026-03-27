using Nexora.Modules.Reporting.Domain.Entities;

namespace Nexora.Modules.Reporting.Tests.Domain;

public sealed class DashboardAdditionalTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    [Fact]
    public void Create_WithNullDescription_ShouldSetDescriptionToNull()
    {
        var dashboard = Dashboard.Create(
            _tenantId, _orgId, "Test Dashboard", null);

        dashboard.Description.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldGenerateUniqueIds()
    {
        var dashboard1 = Dashboard.Create(_tenantId, _orgId, "Dashboard 1", null);
        var dashboard2 = Dashboard.Create(_tenantId, _orgId, "Dashboard 2", null);

        dashboard1.Id.Value.Should().NotBe(dashboard2.Id.Value);
    }

    [Fact]
    public void Update_ShouldTrimStrings()
    {
        var dashboard = Dashboard.Create(_tenantId, _orgId, "Original", null);

        dashboard.Update("  Updated Name  ", "  Updated Desc  ", null, false);

        dashboard.Name.Should().Be("Updated Name");
        dashboard.Description.Should().Be("Updated Desc");
    }

    [Fact]
    public void Update_WithNullWidgets_ShouldClearWidgets()
    {
        var dashboard = Dashboard.Create(_tenantId, _orgId, "Test", null);
        dashboard.Update("Test", null, "[{\"id\":\"1\"}]", false);
        dashboard.Widgets.Should().NotBeNull();

        dashboard.Update("Test", null, null, false);

        dashboard.Widgets.Should().BeNull();
    }

    [Fact]
    public void Update_ShouldChangeIsDefault()
    {
        var dashboard = Dashboard.Create(_tenantId, _orgId, "Test", null, false);

        dashboard.Update("Test", null, null, true);
        dashboard.IsDefault.Should().BeTrue();

        dashboard.Update("Test", null, null, false);
        dashboard.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldSetCorrectOrganizationId()
    {
        var dashboard = Dashboard.Create(_tenantId, _orgId, "Test", null);

        dashboard.OrganizationId.Should().Be(_orgId);
    }
}
