using FluentValidation.TestHelper;
using Nexora.Modules.Contacts.Application.Commands;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class UpdateTagValidatorTests
{
    private readonly UpdateTagValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateTagCommand(Guid.NewGuid(), "Tag", "Donor", "#000"));
        // Act & Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyTagId_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateTagCommand(Guid.Empty, "Tag", "Donor", null));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.TagId);
    }

    [Fact]
    public void Validate_EmptyName_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateTagCommand(Guid.NewGuid(), "", "Donor", null));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_InvalidCategory_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateTagCommand(Guid.NewGuid(), "Tag", "Wrong", null));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Category)
            .WithErrorMessage("lockey_contacts_validation_tag_category_invalid");
    }

    [Fact]
    public void Validate_ColorTooLong_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateTagCommand(Guid.NewGuid(), "Tag", "Donor", new string('x', 21)));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Color);
    }

    [Fact]
    public void Validate_NullColor_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateTagCommand(Guid.NewGuid(), "Tag", "Donor", null));
        // Act & Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Color);
    }
}
