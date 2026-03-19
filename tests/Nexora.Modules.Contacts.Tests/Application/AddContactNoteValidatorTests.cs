using FluentValidation.TestHelper;
using Nexora.Modules.Contacts.Application.Commands;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class AddContactNoteValidatorTests
{
    private readonly AddContactNoteValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new AddContactNoteCommand(
            Guid.NewGuid(), Guid.NewGuid(), "This is a note"));
        // Act & Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyContactId_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new AddContactNoteCommand(
            Guid.Empty, Guid.NewGuid(), "Content"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.ContactId);
    }

    [Fact]
    public void Validate_EmptyAuthorUserId_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new AddContactNoteCommand(
            Guid.NewGuid(), Guid.Empty, "Content"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.AuthorUserId);
    }

    [Fact]
    public void Validate_EmptyContent_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new AddContactNoteCommand(
            Guid.NewGuid(), Guid.NewGuid(), ""));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void Validate_ContentExceedsMaxLength_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new AddContactNoteCommand(
            Guid.NewGuid(), Guid.NewGuid(), new string('a', 5001)));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Content);
    }
}
