using FluentValidation.TestHelper;
using Nexora.Modules.Contacts.Application.Commands;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class SetContactCustomFieldValidatorTests
{
    private readonly SetContactCustomFieldValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new SetContactCustomFieldCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Value"));
        // Act & Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyContactId_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new SetContactCustomFieldCommand(
            Guid.Empty, Guid.NewGuid(), "Value"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.ContactId);
    }

    [Fact]
    public void Validate_EmptyFieldDefinitionId_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new SetContactCustomFieldCommand(
            Guid.NewGuid(), Guid.Empty, "Value"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.FieldDefinitionId);
    }

    [Fact]
    public void Validate_NullValue_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new SetContactCustomFieldCommand(
            Guid.NewGuid(), Guid.NewGuid(), null));
        // Act & Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
