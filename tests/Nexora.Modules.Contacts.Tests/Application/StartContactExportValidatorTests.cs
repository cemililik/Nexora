using Nexora.Modules.Contacts.Application.Commands;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class StartContactExportValidatorTests
{
    private readonly StartContactExportValidator _validator = new();

    [Theory]
    [InlineData("csv")]
    [InlineData("json")]
    [InlineData("xlsx")]
    [InlineData("CSV")]
    [InlineData("JSON")]
    public void Validate_ValidFormat_ShouldPass(string format)
    {
        // Arrange
        var command = new StartContactExportCommand(format);
        var result = _validator.Validate(command);
        // Act & Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("pdf")]
    [InlineData("txt")]
    public void Validate_InvalidFormat_ShouldFail(string format)
    {
        // Arrange
        var command = new StartContactExportCommand(format);
        var result = _validator.Validate(command);
        // Act & Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "lockey_contacts_validation_export_format_invalid");
    }

    [Fact]
    public void Validate_EmptyFormat_ShouldFail()
    {
        // Arrange
        var command = new StartContactExportCommand("");
        var result = _validator.Validate(command);
        // Act & Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "lockey_contacts_validation_export_format_required");
    }
}
