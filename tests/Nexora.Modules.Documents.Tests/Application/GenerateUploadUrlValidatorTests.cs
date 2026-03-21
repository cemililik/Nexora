using Nexora.Modules.Documents.Application.Commands;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class GenerateUploadUrlValidatorTests
{
    private readonly GenerateUploadUrlValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        // Arrange
        var command = new GenerateUploadUrlCommand("report.pdf", "application/pdf", 1024);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyFileName_FailsValidation()
    {
        // Arrange
        var command = new GenerateUploadUrlCommand("", "application/pdf", 1024);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FileName");
    }

    [Fact]
    public void Validate_EmptyContentType_FailsValidation()
    {
        // Arrange
        var command = new GenerateUploadUrlCommand("test.pdf", "", 1024);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ContentType");
    }

    [Fact]
    public void Validate_ZeroFileSize_FailsValidation()
    {
        // Arrange
        var command = new GenerateUploadUrlCommand("test.pdf", "application/pdf", 0);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FileSize");
    }

    [Fact]
    public void Validate_NegativeFileSize_FailsValidation()
    {
        // Arrange
        var command = new GenerateUploadUrlCommand("test.pdf", "application/pdf", -1);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FileSize");
    }

    [Fact]
    public void Validate_ExceedsMaxFileSize_FailsValidation()
    {
        // Arrange
        var command = new GenerateUploadUrlCommand("test.pdf", "application/pdf", 52_428_801);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FileSize");
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("path/to/file.pdf")]
    [InlineData("path\\to\\file.pdf")]
    public void Validate_PathTraversalInFileName_FailsValidation(string fileName)
    {
        var command = new GenerateUploadUrlCommand(fileName, "application/pdf", 1024);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FileName");
    }
}
