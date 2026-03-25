using FluentValidation.TestHelper;
using Nexora.Modules.Identity.Application.Commands;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class CreateTenantValidatorTests
{
    private readonly CreateTenantValidator _validator = new();

    [Fact]
    public void CreateTenantValidator_WithValidData_ValidationPasses()
    {
        var result = _validator.TestValidate(new CreateTenantCommand("Acme", "acme"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateTenantValidator_WithEmptyName_ValidationFails()
    {
        var result = _validator.TestValidate(new CreateTenantCommand("", "acme"));
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("lockey_identity_validation_tenant_name_required");
    }

    [Fact]
    public void CreateTenantValidator_WithEmptySlug_ValidationFails()
    {
        var result = _validator.TestValidate(new CreateTenantCommand("Acme", ""));
        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorMessage("lockey_identity_validation_tenant_slug_required");
    }

    [Fact]
    public void CreateTenantValidator_WithInvalidSlugFormat_ValidationFails()
    {
        var result = _validator.TestValidate(new CreateTenantCommand("Acme", "UPPER CASE!"));
        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorMessage("lockey_identity_validation_tenant_slug_format");
    }

    [Fact]
    public void CreateTenantValidator_WithSlugTooLong_ValidationFails()
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
    public void UpdateTenantStatusValidator_WithValidAction_ValidationPasses(string action)
    {
        var result = _validator.TestValidate(
            new UpdateTenantStatusCommand(Guid.NewGuid(), action));
        result.ShouldNotHaveValidationErrorFor(x => x.Action);
    }

    [Fact]
    public void UpdateTenantStatusValidator_WithInvalidAction_ValidationFails()
    {
        var result = _validator.TestValidate(
            new UpdateTenantStatusCommand(Guid.NewGuid(), "destroy"));
        result.ShouldHaveValidationErrorFor(x => x.Action)
            .WithErrorMessage("lockey_identity_validation_invalid_tenant_action");
    }

    [Fact]
    public void UpdateTenantStatusValidator_WithEmptyTenantId_ValidationFails()
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
    public void CreateUserValidator_WithValidData_ValidationPasses()
    {
        var result = _validator.TestValidate(
            new CreateUserCommand("user@test.com", "John", "Doe", "TempPass1!"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateUserValidator_WithEmptyEmail_ValidationFails()
    {
        var result = _validator.TestValidate(
            new CreateUserCommand("", "John", "Doe", "TempPass1!"));
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("lockey_identity_validation_email_required");
    }

    [Fact]
    public void CreateUserValidator_WithInvalidEmail_ValidationFails()
    {
        var result = _validator.TestValidate(
            new CreateUserCommand("not-an-email", "John", "Doe", "TempPass1!"));
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("lockey_identity_validation_email_format");
    }

    [Fact]
    public void CreateUserValidator_WithEmptyFirstName_ValidationFails()
    {
        var result = _validator.TestValidate(
            new CreateUserCommand("u@t.com", "", "Doe", "TempPass1!"));
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void CreateUserValidator_WithEmptyPassword_ValidationFails()
    {
        var result = _validator.TestValidate(
            new CreateUserCommand("u@t.com", "J", "D", ""));
        result.ShouldHaveValidationErrorFor(x => x.TemporaryPassword)
            .WithErrorMessage("lockey_identity_validation_password_required");
    }

    [Fact]
    public void CreateUserValidator_WithShortPassword_ValidationFails()
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
    public void CreateRoleValidator_WithValidData_ValidationPasses()
    {
        var result = _validator.TestValidate(
            new CreateRoleCommand("Admin", "Full access", null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateRoleValidator_WithEmptyName_ValidationFails()
    {
        var result = _validator.TestValidate(
            new CreateRoleCommand("", null, null));
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("lockey_identity_validation_role_name_required");
    }

    [Fact]
    public void CreateRoleValidator_WithNameTooLong_ValidationFails()
    {
        var result = _validator.TestValidate(
            new CreateRoleCommand(new string('x', 101), null, null));
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("lockey_identity_validation_role_name_max_length");
    }
}

public sealed class UpdateRoleValidatorTests
{
    private readonly UpdateRoleValidator _validator = new();

    [Fact]
    public void UpdateRoleValidator_WithEmptyId_ValidationFails()
    {
        var result = _validator.TestValidate(
            new UpdateRoleCommand(Guid.Empty, "Admin", null, null));
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void UpdateRoleValidator_WithEmptyName_ValidationFails()
    {
        var result = _validator.TestValidate(
            new UpdateRoleCommand(Guid.NewGuid(), "", null, null));
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("lockey_validation_required");
    }

    [Fact]
    public void UpdateRoleValidator_WithNameTooLong_ValidationFails()
    {
        var result = _validator.TestValidate(
            new UpdateRoleCommand(Guid.NewGuid(), new string('x', 101), null, null));
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("lockey_validation_max_length");
    }

    [Fact]
    public void UpdateRoleValidator_WithValidData_ValidationPasses()
    {
        var result = _validator.TestValidate(
            new UpdateRoleCommand(Guid.NewGuid(), "Admin", "Full access", null));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public sealed class DeleteRoleValidatorTests
{
    private readonly DeleteRoleValidator _validator = new();

    [Fact]
    public void DeleteRoleValidator_WithEmptyId_ValidationFails()
    {
        var result = _validator.TestValidate(new DeleteRoleCommand(Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void DeleteRoleValidator_WithValidData_ValidationPasses()
    {
        var result = _validator.TestValidate(new DeleteRoleCommand(Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public sealed class DeleteUserValidatorTests
{
    private readonly DeleteUserValidator _validator = new();

    [Fact]
    public void DeleteUserValidator_WithEmptyId_ValidationFails()
    {
        var result = _validator.TestValidate(new DeleteUserCommand(Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void DeleteUserValidator_WithValidData_ValidationPasses()
    {
        var result = _validator.TestValidate(new DeleteUserCommand(Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public sealed class AssignUserRolesValidatorTests
{
    private readonly AssignUserRolesValidator _validator = new();

    [Fact]
    public void AssignUserRolesValidator_WithEmptyUserId_ValidationFails()
    {
        var result = _validator.TestValidate(
            new AssignUserRolesCommand(Guid.Empty, Guid.NewGuid(), new List<Guid>()));
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void AssignUserRolesValidator_WithEmptyOrganizationId_ValidationFails()
    {
        var result = _validator.TestValidate(
            new AssignUserRolesCommand(Guid.NewGuid(), Guid.Empty, new List<Guid>()));
        result.ShouldHaveValidationErrorFor(x => x.OrganizationId);
    }

    [Fact]
    public void AssignUserRolesValidator_WithNullRoleIds_ValidationFails()
    {
        var result = _validator.TestValidate(
            new AssignUserRolesCommand(Guid.NewGuid(), Guid.NewGuid(), null!));
        result.ShouldHaveValidationErrorFor(x => x.RoleIds)
            .WithErrorMessage("lockey_validation_required");
    }

    [Fact]
    public void AssignUserRolesValidator_WithValidData_ValidationPasses()
    {
        var result = _validator.TestValidate(
            new AssignUserRolesCommand(Guid.NewGuid(), Guid.NewGuid(), new List<Guid> { Guid.NewGuid() }));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public sealed class ActivateModuleValidatorTests
{
    private readonly ActivateModuleValidator _validator = new();

    [Fact]
    public void ActivateModuleValidator_WithEmptyTenantId_ValidationFails()
    {
        var result = _validator.TestValidate(
            new ActivateModuleCommand(Guid.Empty, "CRM"));
        result.ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    [Fact]
    public void ActivateModuleValidator_WithEmptyModuleName_ValidationFails()
    {
        var result = _validator.TestValidate(
            new ActivateModuleCommand(Guid.NewGuid(), ""));
        result.ShouldHaveValidationErrorFor(x => x.ModuleName)
            .WithErrorMessage("lockey_validation_required");
    }
}

public sealed class DeactivateModuleValidatorTests
{
    private readonly DeactivateModuleValidator _validator = new();

    [Fact]
    public void DeactivateModuleValidator_WithEmptyTenantId_ValidationFails()
    {
        var result = _validator.TestValidate(
            new DeactivateModuleCommand(Guid.Empty, "CRM"));
        result.ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    [Fact]
    public void DeactivateModuleValidator_WithEmptyModuleName_ValidationFails()
    {
        var result = _validator.TestValidate(
            new DeactivateModuleCommand(Guid.NewGuid(), ""));
        result.ShouldHaveValidationErrorFor(x => x.ModuleName)
            .WithErrorMessage("lockey_validation_required");
    }
}
