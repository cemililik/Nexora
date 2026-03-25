using FluentValidation.TestHelper;
using Nexora.Modules.Reporting.Application.Commands;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class ValidatorTests
{
    [Fact]
    public void CreateReportDefinition_EmptyName_ShouldHaveError()
    {
        var validator = new CreateReportDefinitionValidator();
        var command = new CreateReportDefinitionCommand(
            "", null, "module", null, "SELECT 1", null, "Csv");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void CreateReportDefinition_EmptyQueryText_ShouldHaveError()
    {
        var validator = new CreateReportDefinitionValidator();
        var command = new CreateReportDefinitionCommand(
            "Name", null, "module", null, "", null, "Csv");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.QueryText);
    }

    [Fact]
    public void CreateReportDefinition_ValidCommand_ShouldNotHaveErrors()
    {
        var validator = new CreateReportDefinitionValidator();
        var command = new CreateReportDefinitionCommand(
            "Revenue", null, "finance", null, "SELECT 1", null, "Csv");

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ExecuteReport_EmptyDefinitionId_ShouldHaveError()
    {
        var validator = new ExecuteReportValidator();
        var command = new ExecuteReportCommand(Guid.Empty, null, null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DefinitionId);
    }

    [Fact]
    public void CreateSchedule_EmptyCron_ShouldHaveError()
    {
        var validator = new CreateReportScheduleValidator();
        var command = new CreateReportScheduleCommand(
            Guid.NewGuid(), "", "Csv", null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.CronExpression);
    }

    [Fact]
    public void CreateDashboard_EmptyName_ShouldHaveError()
    {
        var validator = new CreateDashboardValidator();
        var command = new CreateDashboardCommand("", null, false);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void CreateDashboard_ValidCommand_ShouldNotHaveErrors()
    {
        var validator = new CreateDashboardValidator();
        var command = new CreateDashboardCommand("My Dashboard", "Description", true);

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
