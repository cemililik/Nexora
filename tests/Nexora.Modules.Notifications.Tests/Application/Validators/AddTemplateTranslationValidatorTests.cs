using FluentValidation.TestHelper;
using Nexora.Modules.Notifications.Application.Commands;

namespace Nexora.Modules.Notifications.Tests.Application.Validators;

public sealed class AddTemplateTranslationValidatorTests
{
    private readonly AddTemplateTranslationValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.TestValidate(new AddTemplateTranslationCommand(
            Guid.NewGuid(), "tr", "Konu", "Gövde"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyTemplateId_ShouldFail()
    {
        var result = _validator.TestValidate(new AddTemplateTranslationCommand(
            Guid.Empty, "tr", "Konu", "Gövde"));
        result.ShouldHaveValidationErrorFor(x => x.TemplateId)
            .WithErrorMessage("lockey_notifications_validation_template_id_required");
    }

    [Fact]
    public void Validate_EmptyLanguageCode_ShouldFail()
    {
        var result = _validator.TestValidate(new AddTemplateTranslationCommand(
            Guid.NewGuid(), "", "Konu", "Gövde"));
        result.ShouldHaveValidationErrorFor(x => x.LanguageCode)
            .WithErrorMessage("lockey_notifications_validation_translation_language_required");
    }

    [Fact]
    public void Validate_EmptySubject_ShouldFail()
    {
        var result = _validator.TestValidate(new AddTemplateTranslationCommand(
            Guid.NewGuid(), "tr", "", "Gövde"));
        result.ShouldHaveValidationErrorFor(x => x.Subject)
            .WithErrorMessage("lockey_notifications_validation_template_subject_required");
    }

    [Fact]
    public void Validate_EmptyBody_ShouldFail()
    {
        var result = _validator.TestValidate(new AddTemplateTranslationCommand(
            Guid.NewGuid(), "tr", "Konu", ""));
        result.ShouldHaveValidationErrorFor(x => x.Body)
            .WithErrorMessage("lockey_notifications_validation_template_body_required");
    }
}
