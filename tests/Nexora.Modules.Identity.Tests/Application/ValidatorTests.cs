using FluentValidation.TestHelper;
using Nexora.Modules.Identity.Application.Commands;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class CreateTenantValidatorTests
{
    private readonly CreateTenantValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.TestValidate(new CreateTenantCommand("Acme", "acme"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyName_ShouldFail()
    {
        var result = _validator.TestValidate(new CreateTenantCommand("", "acme"));
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("lockey_identity_validation_tenant_name_required");
    }

    [Fact]
    public void Validate_EmptySlug_ShouldFail()
    {
        var result = _validator.TestValidate(new CreateTenantCommand("Acme", ""));
        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorMessage("lockey_identity_validation_tenant_slug_required");
    }

    [Fact]
    public void Validate_InvalidSlugFormat_ShouldFail()
    {
        var result = _validator.TestValidate(new CreateTenantCommand("Acme", "UPPER CASE!"));
        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorMessage("lockey_identity_validation_tenant_slug_format");
    }

    [Fact]
    public void Validate_SlugTooLong_ShouldFail()
    {
        var result = _validator.TestValidate(
            new CreateTenantCommand("Acme", new string('a', 101)));
        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorMessage("lockey_identity_validation_tenant_slug_max_length");
    }
}

public sealed class UpdateTenantStatusValidatorTests
{
    private readonly UpdateTenantStatusValidator _validator = new();

    [Theory]
    [InlineData("activate")]
    [InlineData("suspend")]
    [InlineData("terminate")]
    public void Validate_ValidAction_ShouldPass(string action)
    {
        var result = _validator.TestValidate(
            new UpdateTenantStatusCommand(Guid.NewGuid(), action));
        result.ShouldNotHaveValidationErrorFor(x => x.Action);
    }

    [Fact]
    public void Validate_InvalidAction_ShouldFail()
    {
        var result = _validator.TestValidate(
            new UpdateTenantStatusCommand(Guid.NewGuid(), "destroy"));
        result.ShouldHaveValidationErrorFor(x => x.Action)
            .WithErrorMessage("lockey_identity_validation_invalid_tenant_action");
    }

    [Fact]
    public void Validate_EmptyTenantId_ShouldFail()
    {
        var result = _validator.TestValidate(
            new UpdateTenantStatusCommand(Guid.Empty, "activate"));
        result.ShouldHaveValidationErrorFor(x => x.TenantId);
    }
}

public sealed class CreateUserValidatorTests
{
    private readonly CreateUserValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.TestValidate(
            new CreateUserCommand("user@test.com", "John", "Doe", "TempPass1!"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyEmail_ShouldFail()
    {
        var result = _validator.TestValidate(
            new CreateUserCommand("", "John", "Doe", "TempPass1!"));
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("lockey_identity_validation_email_required");
    }

    [Fact]
    public void Validate_InvalidEmail_ShouldFail()
    {
        var result = _validator.TestValidate(
            new CreateUserCommand("not-an-email", "John", "Doe", "TempPass1!"));
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("lockey_identity_validation_email_format");
    }

    [Fact]
    public void Validate_EmptyFirstName_ShouldFail()
    {
        var result = _validator.TestValidate(
            new CreateUserCommand("u@t.com", "", "Doe", "TempPass1!"));
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void Validate_EmptyPassword_ShouldFail()
    {
        var result = _validator.TestValidate(
            new CreateUserCommand("u@t.com", "J", "D", ""));
        result.ShouldHaveValidationErrorFor(x => x.TemporaryPassword)
            .WithErrorMessage("lockey_identity_validation_password_required");
    }

    [Fact]
    public void Validate_ShortPassword_ShouldFail()
    {
        var result = _validator.TestValidate(
            new CreateUserCommand("u@t.com", "J", "D", "short"));
        result.ShouldHaveValidationErrorFor(x => x.TemporaryPassword)
            .WithErrorMessage("lockey_identity_validation_password_min_length");
    }
}

public sealed class CreateRoleValidatorTests
{
    private readonly CreateRoleValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.TestValidate(
            new CreateRoleCommand("Admin", "Full access", null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyName_ShouldFail()
    {
        var result = _validator.TestValidate(
            new CreateRoleCommand("", null, null));
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("lockey_identity_validation_role_name_required");
    }

    [Fact]
    public void Validate_NameTooLong_ShouldFail()
    {
        var result = _validator.TestValidate(
            new CreateRoleCommand(new string('x', 101), null, null));
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("lockey_identity_validation_role_name_max_length");
    }
}
