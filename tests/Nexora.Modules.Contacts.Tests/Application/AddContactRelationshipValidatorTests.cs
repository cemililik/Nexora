using FluentValidation.TestHelper;
using Nexora.Modules.Contacts.Application.Commands;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class AddContactRelationshipValidatorTests
{
    private readonly AddContactRelationshipValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new AddContactRelationshipCommand(
            Guid.NewGuid(), Guid.NewGuid(), "ParentOf"));
        // Act & Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyContactId_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new AddContactRelationshipCommand(
            Guid.Empty, Guid.NewGuid(), "ParentOf"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.ContactId);
    }

    [Fact]
    public void Validate_InvalidType_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new AddContactRelationshipCommand(
            Guid.NewGuid(), Guid.NewGuid(), "InvalidType"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Type);
    }

    [Fact]
    public void Validate_SelfRelationship_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var result = _validator.TestValidate(new AddContactRelationshipCommand(id, id, "ParentOf"));
        // Act & Assert
        result.ShouldHaveAnyValidationError()
            .WithErrorMessage("lockey_contacts_validation_self_relationship_not_allowed");
    }
}
