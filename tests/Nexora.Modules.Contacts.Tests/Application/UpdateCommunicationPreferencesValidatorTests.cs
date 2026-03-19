using FluentValidation.TestHelper;
using Nexora.Modules.Contacts.Application.Commands;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class UpdateCommunicationPreferencesValidatorTests
{
    private readonly UpdateCommunicationPreferencesValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateCommunicationPreferencesCommand(
            Guid.NewGuid(), [new("Email", true)]));
        // Act & Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyContactId_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateCommunicationPreferencesCommand(
            Guid.Empty, [new("Email", true)]));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.ContactId);
    }

    [Fact]
    public void Validate_EmptyPreferences_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateCommunicationPreferencesCommand(
            Guid.NewGuid(), []));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Preferences);
    }

    [Fact]
    public void Validate_InvalidChannel_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateCommunicationPreferencesCommand(
            Guid.NewGuid(), [new("InvalidChannel", true)]));
        // Act & Assert
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void Validate_AllChannels_ShouldPass()
    {
        // Arrange
        var channels = new[] { "Email", "Sms", "WhatsApp", "Phone", "Mail" };
        foreach (var channel in channels)
        {
            var result = _validator.TestValidate(new UpdateCommunicationPreferencesCommand(
                Guid.NewGuid(), [new(channel, true)]));
        // Act & Assert
            result.ShouldNotHaveAnyValidationErrors();
        }
    }
}
