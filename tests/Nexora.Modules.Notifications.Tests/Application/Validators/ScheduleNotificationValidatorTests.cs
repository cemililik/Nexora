using FluentValidation.TestHelper;
using Nexora.Modules.Notifications.Application.Commands;

namespace Nexora.Modules.Notifications.Tests.Application.Validators;

public sealed class ScheduleNotificationValidatorTests
{
    private readonly ScheduleNotificationValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var command = new ScheduleNotificationCommand(
            "Email", Guid.NewGuid(), "user@test.com",
            DateTime.UtcNow.AddDays(1),
            Subject: "Test", Body: "Body");

        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyChannel_ShouldFail(string? channel)
    {
        var command = new ScheduleNotificationCommand(
            channel!, Guid.NewGuid(), "user@test.com",
            DateTime.UtcNow.AddDays(1),
            Subject: "Test", Body: "Body");

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Channel);
    }

    [Fact]
    public void Validate_InvalidChannel_ShouldFail()
    {
        var command = new ScheduleNotificationCommand(
            "InvalidChannel", Guid.NewGuid(), "user@test.com",
            DateTime.UtcNow.AddDays(1),
            Subject: "Test", Body: "Body");

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Channel);
    }

    [Fact]
    public void Validate_EmptyContactId_ShouldFail()
    {
        var command = new ScheduleNotificationCommand(
            "Email", Guid.Empty, "user@test.com",
            DateTime.UtcNow.AddDays(1),
            Subject: "Test", Body: "Body");

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ContactId);
    }

    [Fact]
    public void Validate_EmptyRecipientAddress_ShouldFail()
    {
        var command = new ScheduleNotificationCommand(
            "Email", Guid.NewGuid(), "",
            DateTime.UtcNow.AddDays(1),
            Subject: "Test", Body: "Body");

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.RecipientAddress);
    }

    [Fact]
    public void Validate_PastScheduledAt_ShouldFail()
    {
        var command = new ScheduleNotificationCommand(
            "Email", Guid.NewGuid(), "user@test.com",
            DateTime.UtcNow.AddHours(-1),
            Subject: "Test", Body: "Body");

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ScheduledAt);
    }

    [Fact]
    public void Validate_NoTemplateNoContent_ShouldFail()
    {
        var command = new ScheduleNotificationCommand(
            "Email", Guid.NewGuid(), "user@test.com",
            DateTime.UtcNow.AddDays(1));

        var result = _validator.TestValidate(command);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void Validate_WithTemplateCode_ShouldPass()
    {
        var command = new ScheduleNotificationCommand(
            "Email", Guid.NewGuid(), "user@test.com",
            DateTime.UtcNow.AddDays(1),
            TemplateCode: "welcome");

        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
