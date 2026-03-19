using FluentValidation.TestHelper;
using Nexora.Modules.Contacts.Application.Commands;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class CreateCustomFieldDefinitionValidatorTests
{
    private readonly CreateCustomFieldDefinitionValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateCustomFieldDefinitionCommand("Field1", "text"));
        // Act & Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyFieldName_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateCustomFieldDefinitionCommand("", "text"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.FieldName);
    }

    [Fact]
    public void Validate_FieldNameExceedsMaxLength_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateCustomFieldDefinitionCommand(new string('a', 101), "text"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.FieldName);
    }

    [Fact]
    public void Validate_InvalidFieldType_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateCustomFieldDefinitionCommand("Field1", "invalid"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.FieldType);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("number")]
    [InlineData("date")]
    [InlineData("boolean")]
    [InlineData("select")]
    [InlineData("multiselect")]
    public void Validate_AllValidFieldTypes_ShouldPass(string fieldType)
    {
        // Arrange
        var result = _validator.TestValidate(new CreateCustomFieldDefinitionCommand("Field1", fieldType));
        // Act & Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_NegativeDisplayOrder_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateCustomFieldDefinitionCommand("Field1", "text", null, false, -1));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.DisplayOrder);
    }
}
