using FluentValidation.TestHelper;
using Nexora.Modules.Notifications.Application.Commands;

namespace Nexora.Modules.Notifications.Tests.Application.Validators;

public sealed class UpdateNotificationTemplateValidatorTests
{
    private readonly UpdateNotificationTemplateValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.TestValidate(new UpdateNotificationTemplateCommand(
            Guid.NewGuid(), "Updated Subject", "Updated Body", "Html"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyId_ShouldFail()
    {
        var result = _validator.TestValidate(new UpdateNotificationTemplateCommand(
            Guid.Empty, "Subject", "Body", "Html"));
        result.ShouldHaveValidationErrorFor(x => x.Id)
            .WithErrorMessage("lockey_notifications_validation_template_id_required");
    }

    [Fact]
    public void Validate_EmptySubject_ShouldFail()
    {
        var result = _validator.TestValidate(new UpdateNotificationTemplateCommand(
            Guid.NewGuid(), "", "Body", "Html"));
        result.ShouldHaveValidationErrorFor(x => x.Subject)
            .WithErrorMessage("lockey_notifications_validation_template_subject_required");
    }

    [Fact]
    public void Validate_EmptyBody_ShouldFail()
    {
        var result = _validator.TestValidate(new UpdateNotificationTemplateCommand(
            Guid.NewGuid(), "Subject", "", "Html"));
        result.ShouldHaveValidationErrorFor(x => x.Body)
            .WithErrorMessage("lockey_notifications_validation_template_body_required");
    }

    [Fact]
    public void Validate_InvalidFormat_ShouldFail()
    {
        var result = _validator.TestValidate(new UpdateNotificationTemplateCommand(
            Guid.NewGuid(), "Subject", "Body", "Pdf"));
        result.ShouldHaveValidationErrorFor(x => x.Format)
            .WithErrorMessage("lockey_notifications_validation_template_format_invalid");
    }

    [Fact]
    public void Validate_EmptyFormat_ShouldFail()
    {
        var result = _validator.TestValidate(new UpdateNotificationTemplateCommand(
            Guid.NewGuid(), "Subject", "Body", ""));
        result.ShouldHaveValidationErrorFor(x => x.Format)
            .WithErrorMessage("lockey_notifications_validation_template_format_required");
    }
}
