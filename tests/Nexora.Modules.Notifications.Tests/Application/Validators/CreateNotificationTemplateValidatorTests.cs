using FluentValidation.TestHelper;
using Nexora.Modules.Notifications.Application.Commands;

namespace Nexora.Modules.Notifications.Tests.Application.Validators;

public sealed class CreateNotificationTemplateValidatorTests
{
    private readonly CreateNotificationTemplateValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.TestValidate(new CreateNotificationTemplateCommand(
            "welcome_email", "identity", "Email", "Welcome", "<h1>Hello</h1>", "Html"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyCode_ShouldFail()
    {
        var result = _validator.TestValidate(new CreateNotificationTemplateCommand(
            "", "identity", "Email", "Subject", "Body", "Html"));
        result.ShouldHaveValidationErrorFor(x => x.Code)
            .WithErrorMessage("lockey_notifications_validation_template_code_required");
    }

    [Fact]
    public void Validate_InvalidCodeFormat_ShouldFail()
    {
        var result = _validator.TestValidate(new CreateNotificationTemplateCommand(
            "Welcome Email!", "identity", "Email", "Subject", "Body", "Html"));
        result.ShouldHaveValidationErrorFor(x => x.Code)
            .WithErrorMessage("lockey_notifications_validation_template_code_format");
    }

    [Fact]
    public void Validate_EmptyChannel_ShouldFail()
    {
        var result = _validator.TestValidate(new CreateNotificationTemplateCommand(
            "test", "identity", "", "Subject", "Body", "Html"));
        result.ShouldHaveValidationErrorFor(x => x.Channel)
            .WithErrorMessage("lockey_notifications_validation_template_channel_required");
    }

    [Fact]
    public void Validate_InvalidChannel_ShouldFail()
    {
        var result = _validator.TestValidate(new CreateNotificationTemplateCommand(
            "test", "identity", "Pigeon", "Subject", "Body", "Html"));
        result.ShouldHaveValidationErrorFor(x => x.Channel)
            .WithErrorMessage("lockey_notifications_validation_template_channel_invalid");
    }

    [Fact]
    public void Validate_InvalidFormat_ShouldFail()
    {
        var result = _validator.TestValidate(new CreateNotificationTemplateCommand(
            "test", "identity", "Email", "Subject", "Body", "Pdf"));
        result.ShouldHaveValidationErrorFor(x => x.Format)
            .WithErrorMessage("lockey_notifications_validation_template_format_invalid");
    }

    [Fact]
    public void Validate_EmptySubject_ShouldFail()
    {
        var result = _validator.TestValidate(new CreateNotificationTemplateCommand(
            "test", "identity", "Email", "", "Body", "Html"));
        result.ShouldHaveValidationErrorFor(x => x.Subject)
            .WithErrorMessage("lockey_notifications_validation_template_subject_required");
    }

    [Fact]
    public void Validate_EmptyBody_ShouldFail()
    {
        var result = _validator.TestValidate(new CreateNotificationTemplateCommand(
            "test", "identity", "Email", "Subject", "", "Html"));
        result.ShouldHaveValidationErrorFor(x => x.Body)
            .WithErrorMessage("lockey_notifications_validation_template_body_required");
    }

    [Fact]
    public void Validate_EmptyModule_ShouldFail()
    {
        var result = _validator.TestValidate(new CreateNotificationTemplateCommand(
            "test", "", "Email", "Subject", "Body", "Html"));
        result.ShouldHaveValidationErrorFor(x => x.Module)
            .WithErrorMessage("lockey_notifications_validation_template_module_required");
    }
}
