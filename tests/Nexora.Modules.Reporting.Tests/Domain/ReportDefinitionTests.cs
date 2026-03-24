using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Reporting.Tests.Domain;

public sealed class ReportDefinitionTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldSetProperties()
    {
        var definition = ReportDefinition.Create(
            _tenantId, _orgId, "Monthly Revenue", "Revenue report",
            "finance", "financial", "SELECT * FROM orders",
            null, ReportFormat.Csv);

        definition.Id.Value.Should().NotBeEmpty();
        definition.TenantId.Should().Be(_tenantId);
        definition.OrganizationId.Should().Be(_orgId);
        definition.Name.Should().Be("Monthly Revenue");
        definition.Description.Should().Be("Revenue report");
        definition.Module.Should().Be("finance");
        definition.Category.Should().Be("financial");
        definition.QueryText.Should().Be("SELECT * FROM orders");
        definition.Parameters.Should().BeNull();
        definition.DefaultFormat.Should().Be(ReportFormat.Csv);
        definition.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldTrimStrings()
    {
        var definition = ReportDefinition.Create(
            _tenantId, _orgId, "  Report Name  ", "  Description  ",
            "  module  ", "  category  ", "SELECT 1",
            null, ReportFormat.Json);

        definition.Name.Should().Be("Report Name");
        definition.Description.Should().Be("Description");
        definition.Module.Should().Be("module");
        definition.Category.Should().Be("category");
    }

    [Fact]
    public void Update_ShouldChangeProperties()
    {
        var definition = ReportDefinition.Create(
            _tenantId, _orgId, "Old Name", null,
            "old", null, "SELECT 1", null, ReportFormat.Csv);

        definition.Update("New Name", "New Desc", "new", "cat",
            "SELECT 2", "[{\"name\":\"id\"}]", ReportFormat.Excel);

        definition.Name.Should().Be("New Name");
        definition.Description.Should().Be("New Desc");
        definition.Module.Should().Be("new");
        definition.Category.Should().Be("cat");
        definition.QueryText.Should().Be("SELECT 2");
        definition.Parameters.Should().NotBeNull();
        definition.DefaultFormat.Should().Be(ReportFormat.Excel);
    }

    [Fact]
    public void Deactivate_ShouldSetInactive()
    {
        var definition = ReportDefinition.Create(
            _tenantId, _orgId, "Test", null, "mod", null,
            "SELECT 1", null, ReportFormat.Csv);

        definition.Deactivate();

        definition.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldThrow()
    {
        var definition = ReportDefinition.Create(
            _tenantId, _orgId, "Test", null, "mod", null,
            "SELECT 1", null, ReportFormat.Csv);
        definition.Deactivate();

        var act = () => definition.Deactivate();

        act.Should().Throw<DomainException>()
            .WithMessage("lockey_reporting_error_definition_already_inactive");
    }

    [Fact]
    public void Activate_WhenInactive_ShouldSetActive()
    {
        var definition = ReportDefinition.Create(
            _tenantId, _orgId, "Test", null, "mod", null,
            "SELECT 1", null, ReportFormat.Csv);
        definition.Deactivate();

        definition.Activate();

        definition.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ShouldThrow()
    {
        var definition = ReportDefinition.Create(
            _tenantId, _orgId, "Test", null, "mod", null,
            "SELECT 1", null, ReportFormat.Csv);

        var act = () => definition.Activate();

        act.Should().Throw<DomainException>()
            .WithMessage("lockey_reporting_error_definition_already_active");
    }
}
