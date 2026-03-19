using FluentValidation.TestHelper;
using Nexora.Modules.Contacts.Application.Commands;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class UpdateContactNoteValidatorTests
{
    private readonly UpdateContactNoteValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateContactNoteCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Updated content"));
        // Act & Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyContactId_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateContactNoteCommand(
            Guid.Empty, Guid.NewGuid(), "Content"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.ContactId);
    }

    [Fact]
    public void Validate_EmptyNoteId_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateContactNoteCommand(
            Guid.NewGuid(), Guid.Empty, "Content"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.NoteId);
    }

    [Fact]
    public void Validate_EmptyContent_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new UpdateContactNoteCommand(
            Guid.NewGuid(), Guid.NewGuid(), ""));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Content);
    }
}
