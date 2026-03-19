using FluentValidation.TestHelper;
using Nexora.Modules.Contacts.Application.Commands;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class UpdateContactValidatorTests
{
    private readonly UpdateContactValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateContactCommand(
            Guid.NewGuid(), "John", "Doe", null, "john@test.com", null, null, null, null, "en", "USD"));
        // Act & Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyContactId_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateContactCommand(
            Guid.Empty, "John", "Doe", null, null, null, null, null, null, "en", "USD"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.ContactId);
    }

    [Fact]
    public void Validate_EmptyLanguage_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateContactCommand(
            Guid.NewGuid(), "John", "Doe", null, null, null, null, null, null, "", "USD"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Language);
    }

    [Fact]
    public void Validate_EmptyCurrency_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateContactCommand(
            Guid.NewGuid(), "John", "Doe", null, null, null, null, null, null, "en", ""));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Validate_InvalidCurrencyLength_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateContactCommand(
            Guid.NewGuid(), "John", "Doe", null, null, null, null, null, null, "en", "USDX"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Validate_InvalidEmail_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateContactCommand(
            Guid.NewGuid(), "John", "Doe", null, "bad-email", null, null, null, null, "en", "USD"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}
