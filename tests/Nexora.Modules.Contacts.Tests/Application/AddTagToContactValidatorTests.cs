using FluentValidation.TestHelper;
using Nexora.Modules.Contacts.Application.Commands;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class AddTagToContactValidatorTests
{
    private readonly AddTagToContactValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new AddTagToContactCommand(Guid.NewGuid(), Guid.NewGuid()));
        // Act & Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyContactId_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new AddTagToContactCommand(Guid.Empty, Guid.NewGuid()));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.ContactId);
    }

    [Fact]
    public void Validate_EmptyTagId_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new AddTagToContactCommand(Guid.NewGuid(), Guid.Empty));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.TagId);
    }
}
