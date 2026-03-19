using Nexora.Modules.Contacts.Application.Commands;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class StartContactImportValidatorTests
{
    private readonly StartContactImportValidator _validator = new();

    [Theory]
    [InlineData("csv")]
    [InlineData("xlsx")]
    [InlineData("CSV")]
    [InlineData("XLSX")]
    public void Validate_ValidFormat_ShouldPass(string format)
    {
        // Arrange
        var command = new StartContactImportCommand("test.csv", format, new byte[] { 1, 2, 3 });
        var result = _validator.Validate(command);
        // Act & Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("json")]
    [InlineData("xml")]
    [InlineData("pdf")]
    public void Validate_InvalidFormat_ShouldFail(string format)
    {
        // Arrange
        var command = new StartContactImportCommand("test.csv", format, new byte[] { 1, 2, 3 });
        var result = _validator.Validate(command);
        // Act & Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "lockey_contacts_validation_import_format_invalid");
    }

    [Fact]
    public void Validate_EmptyFormat_ShouldFail()
    {
        // Arrange
        var command = new StartContactImportCommand("test.csv", "", new byte[] { 1, 2, 3 });
        var result = _validator.Validate(command);
        // Act & Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyFileName_ShouldFail()
    {
        // Arrange
        var command = new StartContactImportCommand("", "csv", new byte[] { 1, 2, 3 });
        var result = _validator.Validate(command);
        // Act & Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "lockey_contacts_validation_import_filename_required");
    }

    [Fact]
    public void Validate_EmptyFileContent_ShouldFail()
    {
        // Arrange
        var command = new StartContactImportCommand("test.csv", "csv", []);
        var result = _validator.Validate(command);
        // Act & Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "lockey_contacts_validation_import_file_empty");
    }

    [Fact]
    public void Validate_FileTooLarge_ShouldFail()
    {
        // Arrange
        var largeContent = new byte[11 * 1024 * 1024]; // 11 MB
        var command = new StartContactImportCommand("test.csv", "csv", largeContent);
        var result = _validator.Validate(command);
        // Act & Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "lockey_contacts_validation_import_file_too_large");
    }
}
