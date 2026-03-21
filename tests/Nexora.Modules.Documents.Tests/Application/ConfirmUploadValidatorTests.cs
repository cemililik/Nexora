using Nexora.Modules.Documents.Application.Commands;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class ConfirmUploadValidatorTests
{
    private readonly ConfirmUploadValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        // Arrange
        var command = new ConfirmUploadCommand(
            Guid.NewGuid(), "storage/key", "report.pdf", "application/pdf", 1024);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyFolderId_FailsValidation()
    {
        // Arrange
        var command = new ConfirmUploadCommand(
            Guid.Empty, "storage/key", "test.pdf", "application/pdf", 1024);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FolderId");
    }

    [Fact]
    public void Validate_EmptyStorageKey_FailsValidation()
    {
        // Arrange
        var command = new ConfirmUploadCommand(
            Guid.NewGuid(), "", "test.pdf", "application/pdf", 1024);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StorageKey");
    }

    [Fact]
    public void Validate_EmptyName_FailsValidation()
    {
        // Arrange
        var command = new ConfirmUploadCommand(
            Guid.NewGuid(), "storage/key", "", "application/pdf", 1024);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_ZeroFileSize_FailsValidation()
    {
        // Arrange
        var command = new ConfirmUploadCommand(
            Guid.NewGuid(), "storage/key", "test.pdf", "application/pdf", 0);

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
        var command = new ConfirmUploadCommand(
            Guid.NewGuid(), "storage/key", "test.pdf", "application/pdf", 52_428_801);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FileSize");
    }
}
