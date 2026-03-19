using FluentValidation.TestHelper;
using Nexora.Modules.Identity.Application.Commands;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class CreateTenantValidatorTests
{
    private readonly CreateTenantValidator _validator = new();

    [Fact]
    public void Valid_ShouldPass()
    {
        var result = _validator.TestValidate(new CreateTenantCommand("Acme", "acme"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyName_ShouldFail()
    {
        var result = _validator.TestValidate(new CreateTenantCommand("", "acme"));
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("lockey_identity_validation_tenant_name_required");
    }

    [Fact]
    public void EmptySlug_ShouldFail()
    {
        var result = _validator.TestValidate(new CreateTenantCommand("Acme", ""));
        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorMessage("lockey_identity_validation_tenant_slug_required");
    }

    [Fact]
    public void InvalidSlugFormat_ShouldFail()
    {
        var result = _validator.TestValidate(new CreateTenantCommand("Acme", "UPPER CASE!"));
        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorMessage("lockey_identity_validation_tenant_slug_format");
    }

    [Fact]
    public void SlugTooLong_ShouldFail()
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
    public void ValidAction_ShouldPass(string action)
    {
        var result = _validator.TestValidate(
            new UpdateTenantStatusCommand(Guid.NewGuid(), action));
        result.ShouldNotHaveValidationErrorFor(x => x.Action);
    }

    [Fact]
    public void InvalidAction_ShouldFail()
    {
        var result = _validator.TestValidate(
            new UpdateTenantStatusCommand(Guid.NewGuid(), "destroy"));
        result.ShouldHaveValidationErrorFor(x => x.Action)
            .WithErrorMessage("lockey_identity_validation_invalid_tenant_action");
    }

    [Fact]
    public void EmptyTenantId_ShouldFail()
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
    public void Valid_ShouldPass()
    {
        var result = _validator.TestValidate(
            new CreateUserCommand("kc-1", "user@test.com", "John", "Doe"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyEmail_ShouldFail()
    {
        var result = _validator.TestValidate(
            new CreateUserCommand("kc-1", "", "John", "Doe"));
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("lockey_identity_validation_email_required");
    }

    [Fact]
    public void InvalidEmail_ShouldFail()
    {
        var result = _validator.TestValidate(
            new CreateUserCommand("kc-1", "not-an-email", "John", "Doe"));
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("lockey_identity_validation_email_format");
    }

    [Fact]
    public void EmptyFirstName_ShouldFail()
    {
        var result = _validator.TestValidate(
            new CreateUserCommand("kc-1", "u@t.com", "", "Doe"));
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void EmptyKeycloakId_ShouldFail()
    {
        var result = _validator.TestValidate(
            new CreateUserCommand("", "u@t.com", "J", "D"));
        result.ShouldHaveValidationErrorFor(x => x.KeycloakUserId);
    }
}

public sealed class CreateRoleValidatorTests
{
    private readonly CreateRoleValidator _validator = new();

    [Fact]
    public void Valid_ShouldPass()
    {
        var result = _validator.TestValidate(
            new CreateRoleCommand("Admin", "Full access", null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyName_ShouldFail()
    {
        var result = _validator.TestValidate(
            new CreateRoleCommand("", null, null));
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("lockey_identity_validation_role_name_required");
    }

    [Fact]
    public void NameTooLong_ShouldFail()
    {
        var result = _validator.TestValidate(
            new CreateRoleCommand(new string('x', 101), null, null));
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("lockey_identity_validation_role_name_max_length");
    }
}
