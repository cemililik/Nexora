using FluentValidation.TestHelper;
using Nexora.Modules.Notifications.Application.Commands;

namespace Nexora.Modules.Notifications.Tests.Application.Validators;

public sealed class SendBulkNotificationValidatorTests
{
    private readonly SendBulkNotificationValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var command = new SendBulkNotificationCommand(
            "Email",
            [new BulkRecipient(Guid.NewGuid(), "a@test.com")],
            Subject: "Test", Body: "Body");

        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyChannel_ShouldFail(string? channel)
    {
        var command = new SendBulkNotificationCommand(
            channel!,
            [new BulkRecipient(Guid.NewGuid(), "a@test.com")],
            Subject: "Test", Body: "Body");

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Channel);
    }

    [Fact]
    public void Validate_InvalidChannel_ShouldFail()
    {
        var command = new SendBulkNotificationCommand(
            "InvalidChannel",
            [new BulkRecipient(Guid.NewGuid(), "a@test.com")],
            Subject: "Test", Body: "Body");

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Channel);
    }

    [Fact]
    public void Validate_EmptyRecipients_ShouldFail()
    {
        var command = new SendBulkNotificationCommand(
            "Email",
            [],
            Subject: "Test", Body: "Body");

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Recipients);
    }

    [Fact]
    public void Validate_NoTemplateNoContent_ShouldFail()
    {
        var command = new SendBulkNotificationCommand(
            "Email",
            [new BulkRecipient(Guid.NewGuid(), "a@test.com")]);

        var result = _validator.TestValidate(command);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void Validate_WithTemplateCode_ShouldPass()
    {
        var command = new SendBulkNotificationCommand(
            "Email",
            [new BulkRecipient(Guid.NewGuid(), "a@test.com")],
            TemplateCode: "welcome");

        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
