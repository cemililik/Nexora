using FluentValidation.TestHelper;
using Nexora.Modules.Contacts.Application.Commands;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class CreateContactValidatorTests
{
    private readonly CreateContactValidator _validator = new();

    [Fact]
    public void Validate_ValidIndividual_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateContactCommand("Individual", "John", "Doe", null, "john@test.com", null, "Manual"));
        // Act & Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ValidOrganization_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateContactCommand("Organization", null, null, "Acme Corp", null, null, "Api"));
        // Act & Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyType_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateContactCommand("", "John", "Doe", null, null, null, "Manual"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Type);
    }

    [Fact]
    public void Validate_InvalidType_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateContactCommand("Unknown", "John", "Doe", null, null, null, "Manual"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Type)
            .WithErrorMessage("lockey_contacts_validation_type_invalid");
    }

    [Fact]
    public void Validate_IndividualWithoutFirstName_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateContactCommand("Individual", null, "Doe", null, null, null, "Manual"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.FirstName)
            .WithErrorMessage("lockey_contacts_validation_first_name_required");
    }

    [Fact]
    public void Validate_IndividualWithoutLastName_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateContactCommand("Individual", "John", null, null, null, null, "Manual"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.LastName)
            .WithErrorMessage("lockey_contacts_validation_last_name_required");
    }

    [Fact]
    public void Validate_OrganizationWithoutCompanyName_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateContactCommand("Organization", null, null, null, null, null, "Manual"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.CompanyName)
            .WithErrorMessage("lockey_contacts_validation_company_name_required");
    }

    [Fact]
    public void Validate_InvalidEmail_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateContactCommand("Individual", "John", "Doe", null, "not-an-email", null, "Manual"));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_EmptySource_ShouldFail()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateContactCommand("Individual", "John", "Doe", null, null, null, ""));
        // Act & Assert
        result.ShouldHaveValidationErrorFor(x => x.Source);
    }

    [Fact]
    public void Validate_NullEmail_ShouldPass()
    {
        // Arrange
        var result = _validator.TestValidate(new CreateContactCommand("Individual", "John", "Doe", null, null, null, "Manual"));
        // Act & Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }
}
