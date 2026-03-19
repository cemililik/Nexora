using FluentValidation.TestHelper;
using Nexora.Modules.Contacts.Application.Commands;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class MergeContactsValidatorTests
{
    private readonly MergeContactsValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new MergeContactsCommand(Guid.NewGuid(), Guid.NewGuid()));
        // Act & Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyPrimaryId_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new MergeContactsCommand(Guid.Empty, Guid.NewGuid()));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.PrimaryContactId);
    }

    [Fact]
    public void Validate_EmptySecondaryId_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new MergeContactsCommand(Guid.NewGuid(), Guid.Empty));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.SecondaryContactId);
    }

    [Fact]
    public void Validate_SameIds_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var result = _validator.TestValidate(new MergeContactsCommand(id, id));
        // Act & Assert
        result.ShouldHaveAnyValidationError()
            .WithErrorMessage("lockey_contacts_validation_cannot_merge_same_contact");
    }
}
