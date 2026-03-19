using FluentValidation.TestHelper;
using Nexora.Modules.Contacts.Application.Commands;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class RecordConsentValidatorTests
{
    private readonly RecordConsentValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new RecordConsentCommand(
            Guid.NewGuid(), "EmailMarketing", true, "Web"));
        // Act & Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyContactId_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new RecordConsentCommand(
            Guid.Empty, "EmailMarketing", true));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.ContactId);
    }

    [Fact]
    public void Validate_EmptyConsentType_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new RecordConsentCommand(
            Guid.NewGuid(), "", true));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.ConsentType);
    }

    [Fact]
    public void Validate_InvalidConsentType_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new RecordConsentCommand(
            Guid.NewGuid(), "InvalidType", true));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.ConsentType);
    }

    [Fact]
    public void Validate_AllConsentTypes_ShouldPass()
    {
        // Arrange
        var types = new[] { "EmailMarketing", "SmsMarketing", "DataProcessing" };
        foreach (var type in types)
        {
            var result = _validator.TestValidate(new RecordConsentCommand(
                Guid.NewGuid(), type, true));
        // Act & Assert
            result.ShouldNotHaveAnyValidationErrors();
        }
    }

    [Fact]
    public void Validate_SourceExceedsMaxLength_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new RecordConsentCommand(
            Guid.NewGuid(), "EmailMarketing", true, new string('a', 201)));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Source);
    }
}
