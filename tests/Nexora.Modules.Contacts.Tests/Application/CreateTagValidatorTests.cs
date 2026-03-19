using FluentValidation.TestHelper;
using Nexora.Modules.Contacts.Application.Commands;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class CreateTagValidatorTests
{
    private readonly CreateTagValidator _validator = new();

    [Fact]
    public void Validate_ValidTag_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateTagCommand("VIP", "Donor", "#FF0000"));
        // Act & Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyName_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateTagCommand("", "Donor"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_NameTooLong_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateTagCommand(new string('a', 101), "Donor"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_EmptyCategory_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateTagCommand("Tag", ""));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Category);
    }

    [Fact]
    public void Validate_InvalidCategory_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateTagCommand("Tag", "InvalidCategory"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Category)
            .WithErrorMessage("lockey_contacts_validation_tag_category_invalid");
    }

    [Fact]
    public void Validate_ColorTooLong_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateTagCommand("Tag", "Donor", new string('a', 21)));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Color);
    }

    [Fact]
    public void Validate_NullColor_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateTagCommand("Tag", "Donor"));
        // Act & Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Color);
    }

    [Fact]
    public void Validate_AllCategories_ShouldPass()
    {
        // Arrange
        var categories = new[] { "Donor", "Parent", "Volunteer", "Vendor", "Student", "Staff" };
        foreach (var category in categories)
        {
            var result = _validator.TestValidate(new CreateTagCommand("Tag", category));
        // Act & Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Category);
        }
    }
}
