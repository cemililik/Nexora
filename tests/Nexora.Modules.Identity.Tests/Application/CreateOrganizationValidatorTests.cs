using FluentValidation.TestHelper;
using Nexora.Modules.Identity.Application.Commands;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class CreateOrganizationValidatorTests
{
    private readonly CreateOrganizationValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var command = new CreateOrganizationCommand("Acme School", "acme-school");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyName_ShouldFail()
    {
        var command = new CreateOrganizationCommand("", "slug");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("lockey_identity_validation_org_name_required");
    }

    [Fact]
    public void Validate_NameTooLong_ShouldFail()
    {
        var command = new CreateOrganizationCommand(new string('a', 201), "slug");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("lockey_identity_validation_org_name_max_length");
    }

    [Fact]
    public void Validate_EmptySlug_ShouldFail()
    {
        var command = new CreateOrganizationCommand("Name", "");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorMessage("lockey_identity_validation_org_slug_required");
    }

    [Fact]
    public void Validate_SlugWithUpperCase_ShouldFail()
    {
        var command = new CreateOrganizationCommand("Name", "INVALID-SLUG");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorMessage("lockey_identity_validation_org_slug_format");
    }

    [Fact]
    public void Validate_SlugWithSpaces_ShouldFail()
    {
        var command = new CreateOrganizationCommand("Name", "has spaces");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorMessage("lockey_identity_validation_org_slug_format");
    }

    [Theory]
    [InlineData("valid-slug")]
    [InlineData("valid123")]
    [InlineData("a-b-c")]
    [InlineData("123")]
    public void Validate_ValidSlugFormats_ShouldPass(string slug)
    {
        var command = new CreateOrganizationCommand("Name", slug);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Slug);
    }
}
