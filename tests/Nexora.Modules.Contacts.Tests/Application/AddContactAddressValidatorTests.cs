using FluentValidation.TestHelper;
using Nexora.Modules.Contacts.Application.Commands;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class AddContactAddressValidatorTests
{
    private readonly AddContactAddressValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new AddContactAddressCommand(
            Guid.NewGuid(), "Home", "123 Main St", "Istanbul", "TR"));
        // Act & Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyContactId_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new AddContactAddressCommand(
            Guid.Empty, "Home", "St", "City", "TR"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.ContactId);
    }

    [Fact]
    public void Validate_InvalidType_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new AddContactAddressCommand(
            Guid.NewGuid(), "Invalid", "St", "City", "TR"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Type);
    }

    [Fact]
    public void Validate_EmptyStreet_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new AddContactAddressCommand(
            Guid.NewGuid(), "Home", "", "City", "TR"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Street1);
    }

    [Fact]
    public void Validate_InvalidCountryCodeLength_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new AddContactAddressCommand(
            Guid.NewGuid(), "Home", "St", "City", "TUR"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.CountryCode);
    }
}
