using FluentValidation.TestHelper;
using Nexora.Modules.Contacts.Application.Commands;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class LogContactActivityValidatorTests
{
    private readonly LogContactActivityValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new LogContactActivityCommand(
            Guid.NewGuid(), "contacts", "Created", "Contact created"));
        // Act & Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyContactId_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new LogContactActivityCommand(
            Guid.Empty, "contacts", "Created", "Summary"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.ContactId);
    }

    [Fact]
    public void Validate_EmptyModuleSource_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new LogContactActivityCommand(
            Guid.NewGuid(), "", "Created", "Summary"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.ModuleSource);
    }

    [Fact]
    public void Validate_EmptyActivityType_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new LogContactActivityCommand(
            Guid.NewGuid(), "contacts", "", "Summary"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.ActivityType);
    }

    [Fact]
    public void Validate_EmptySummary_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new LogContactActivityCommand(
            Guid.NewGuid(), "contacts", "Created", ""));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Summary);
    }

    [Fact]
    public void Validate_SummaryExceedsMaxLength_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new LogContactActivityCommand(
            Guid.NewGuid(), "contacts", "Created", new string('a', 501)));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Summary);
    }

    [Fact]
    public void Validate_DetailsExceedsMaxLength_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new LogContactActivityCommand(
            Guid.NewGuid(), "contacts", "Created", "Summary", new string('a', 5001)));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Details);
    }
}
