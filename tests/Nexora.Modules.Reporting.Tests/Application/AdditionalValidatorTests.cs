using FluentValidation.TestHelper;
using Nexora.Modules.Reporting.Application.Commands;

namespace Nexora.Modules.Reporting.Tests.Application;

public sealed class AdditionalValidatorTests
{
    // -- UpdateReportDefinition Validator --

    [Fact]
    public void UpdateReportDefinition_EmptyId_ShouldHaveError()
    {
        var validator = new UpdateReportDefinitionValidator();
        var command = new UpdateReportDefinitionCommand(
            Guid.Empty, "Name", null, "mod", null, "SELECT 1", null, "Csv");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void UpdateReportDefinition_EmptyName_ShouldHaveError()
    {
        var validator = new UpdateReportDefinitionValidator();
        var command = new UpdateReportDefinitionCommand(
            Guid.NewGuid(), "", null, "mod", null, "SELECT 1", null, "Csv");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void UpdateReportDefinition_NameExceedsMaxLength_ShouldHaveError()
    {
        var validator = new UpdateReportDefinitionValidator();
        var command = new UpdateReportDefinitionCommand(
            Guid.NewGuid(), new string('A', 201), null, "mod", null, "SELECT 1", null, "Csv");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void UpdateReportDefinition_EmptyModule_ShouldHaveError()
    {
        var validator = new UpdateReportDefinitionValidator();
        var command = new UpdateReportDefinitionCommand(
            Guid.NewGuid(), "Name", null, "", null, "SELECT 1", null, "Csv");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Module);
    }

    [Fact]
    public void UpdateReportDefinition_EmptyQueryText_ShouldHaveError()
    {
        var validator = new UpdateReportDefinitionValidator();
        var command = new UpdateReportDefinitionCommand(
            Guid.NewGuid(), "Name", null, "mod", null, "", null, "Csv");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.QueryText);
    }

    [Fact]
    public void UpdateReportDefinition_EmptyDefaultFormat_ShouldHaveError()
    {
        var validator = new UpdateReportDefinitionValidator();
        var command = new UpdateReportDefinitionCommand(
            Guid.NewGuid(), "Name", null, "mod", null, "SELECT 1", null, "");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DefaultFormat);
    }

    [Fact]
    public void UpdateReportDefinition_ValidCommand_ShouldNotHaveErrors()
    {
        var validator = new UpdateReportDefinitionValidator();
        var command = new UpdateReportDefinitionCommand(
            Guid.NewGuid(), "Revenue", null, "finance", null, "SELECT 1", null, "Csv");

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    // -- UpdateDashboard Validator --

    [Fact]
    public void UpdateDashboard_EmptyId_ShouldHaveError()
    {
        var validator = new UpdateDashboardValidator();
        var command = new UpdateDashboardCommand(Guid.Empty, "Name", null, null, false);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void UpdateDashboard_EmptyName_ShouldHaveError()
    {
        var validator = new UpdateDashboardValidator();
        var command = new UpdateDashboardCommand(Guid.NewGuid(), "", null, null, false);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void UpdateDashboard_NameExceedsMaxLength_ShouldHaveError()
    {
        var validator = new UpdateDashboardValidator();
        var command = new UpdateDashboardCommand(
            Guid.NewGuid(), new string('A', 201), null, null, false);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void UpdateDashboard_ValidCommand_ShouldNotHaveErrors()
    {
        var validator = new UpdateDashboardValidator();
        var command = new UpdateDashboardCommand(
            Guid.NewGuid(), "Dashboard", "Desc", null, true);

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    // -- UpdateReportSchedule Validator --

    [Fact]
    public void UpdateReportSchedule_EmptyId_ShouldHaveError()
    {
        var validator = new UpdateReportScheduleValidator();
        var command = new UpdateReportScheduleCommand(
            Guid.Empty, "0 0 * * *", "Csv", null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void UpdateReportSchedule_EmptyCron_ShouldHaveError()
    {
        var validator = new UpdateReportScheduleValidator();
        var command = new UpdateReportScheduleCommand(
            Guid.NewGuid(), "", "Csv", null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.CronExpression);
    }

    [Fact]
    public void UpdateReportSchedule_EmptyFormat_ShouldHaveError()
    {
        var validator = new UpdateReportScheduleValidator();
        var command = new UpdateReportScheduleCommand(
            Guid.NewGuid(), "0 0 * * *", "", null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Format);
    }

    [Fact]
    public void UpdateReportSchedule_ValidCommand_ShouldNotHaveErrors()
    {
        var validator = new UpdateReportScheduleValidator();
        var command = new UpdateReportScheduleCommand(
            Guid.NewGuid(), "0 8 * * 1", "Excel", "[\"admin@test.com\"]");

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    // -- Delete Validators --

    [Fact]
    public void DeleteDashboard_EmptyId_ShouldHaveError()
    {
        var validator = new DeleteDashboardValidator();
        var command = new DeleteDashboardCommand(Guid.Empty);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void DeleteDashboard_ValidId_ShouldNotHaveErrors()
    {
        var validator = new DeleteDashboardValidator();
        var command = new DeleteDashboardCommand(Guid.NewGuid());

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DeleteReportSchedule_EmptyId_ShouldHaveError()
    {
        var validator = new DeleteReportScheduleValidator();
        var command = new DeleteReportScheduleCommand(Guid.Empty);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void DeleteReportSchedule_ValidId_ShouldNotHaveErrors()
    {
        var validator = new DeleteReportScheduleValidator();
        var command = new DeleteReportScheduleCommand(Guid.NewGuid());

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DeleteReportDefinition_EmptyId_ShouldHaveError()
    {
        var validator = new DeleteReportDefinitionValidator();
        var command = new DeleteReportDefinitionCommand(Guid.Empty);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void DeleteReportDefinition_ValidId_ShouldNotHaveErrors()
    {
        var validator = new DeleteReportDefinitionValidator();
        var command = new DeleteReportDefinitionCommand(Guid.NewGuid());

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    // -- CreateReportSchedule Validator --

    [Fact]
    public void CreateReportSchedule_EmptyDefinitionId_ShouldHaveError()
    {
        var validator = new CreateReportScheduleValidator();
        var command = new CreateReportScheduleCommand(
            Guid.Empty, "0 0 * * *", "Csv", null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DefinitionId);
    }

    [Fact]
    public void CreateReportSchedule_EmptyFormat_ShouldHaveError()
    {
        var validator = new CreateReportScheduleValidator();
        var command = new CreateReportScheduleCommand(
            Guid.NewGuid(), "0 0 * * *", "", null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Format);
    }

    [Fact]
    public void CreateReportSchedule_ValidCommand_ShouldNotHaveErrors()
    {
        var validator = new CreateReportScheduleValidator();
        var command = new CreateReportScheduleCommand(
            Guid.NewGuid(), "0 8 * * 1", "Excel", null);

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    // -- CreateReportDefinition Validator (additional) --

    [Fact]
    public void CreateReportDefinition_EmptyModule_ShouldHaveError()
    {
        var validator = new CreateReportDefinitionValidator();
        var command = new CreateReportDefinitionCommand(
            "Name", null, "", null, "SELECT 1", null, "Csv");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Module);
    }

    [Fact]
    public void CreateReportDefinition_EmptyDefaultFormat_ShouldHaveError()
    {
        var validator = new CreateReportDefinitionValidator();
        var command = new CreateReportDefinitionCommand(
            "Name", null, "mod", null, "SELECT 1", null, "");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DefaultFormat);
    }

    [Fact]
    public void CreateReportDefinition_NameExceedsMaxLength_ShouldHaveError()
    {
        var validator = new CreateReportDefinitionValidator();
        var command = new CreateReportDefinitionCommand(
            new string('A', 201), null, "mod", null, "SELECT 1", null, "Csv");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}
