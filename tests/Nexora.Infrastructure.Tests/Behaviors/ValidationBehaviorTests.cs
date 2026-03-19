using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Nexora.Infrastructure.Behaviors;
using Nexora.SharedKernel.Results;
using NSubstitute;

namespace Nexora.Infrastructure.Tests.Behaviors;

// Test request/response types
public sealed record TestCommand(string Name, string Email) : IRequest<Result<string>>;

public sealed class TestCommandValidator : AbstractValidator<TestCommand>
{
    public TestCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("lockey_test_name_required");
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("lockey_test_email_required")
            .EmailAddress().WithMessage("lockey_test_email_format");
    }
}

// Non-Result response type for testing the throw path
public sealed record TestNonResultCommand(string Name) : IRequest<string>;

public sealed class TestNonResultCommandValidator : AbstractValidator<TestNonResultCommand>
{
    public TestNonResultCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("lockey_test_name_required");
    }
}

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_NoValidators_ShouldCallNext()
    {
        var behavior = new ValidationBehavior<TestCommand, Result<string>>(
            Enumerable.Empty<IValidator<TestCommand>>());

        var nextCalled = false;
        RequestHandlerDelegate<Result<string>> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result<string>.Success("ok"));
        };

        await behavior.Handle(new TestCommand("test", "a@b.com"), next, CancellationToken.None);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidRequest_ShouldCallNext()
    {
        var validators = new[] { new TestCommandValidator() };
        var behavior = new ValidationBehavior<TestCommand, Result<string>>(validators);

        var nextCalled = false;
        RequestHandlerDelegate<Result<string>> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result<string>.Success("ok"));
        };

        await behavior.Handle(new TestCommand("valid-name", "test@test.com"), next, CancellationToken.None);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_InvalidRequest_ShouldReturnResultFailure()
    {
        var validators = new[] { new TestCommandValidator() };
        var behavior = new ValidationBehavior<TestCommand, Result<string>>(validators);

        var nextCalled = false;
        RequestHandlerDelegate<Result<string>> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result<string>.Success("ok"));
        };

        var result = await behavior.Handle(new TestCommand("", "a@b.com"), next, CancellationToken.None);

        nextCalled.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Key.Should().Be("lockey_validation_failed");
    }

    [Fact]
    public async Task Handle_MultipleFailures_ShouldReturnAllErrorsInDetails()
    {
        var validators = new[] { new TestCommandValidator() };
        var behavior = new ValidationBehavior<TestCommand, Result<string>>(validators);

        RequestHandlerDelegate<Result<string>> next = () =>
            Task.FromResult(Result<string>.Success("ok"));

        // Both Name and Email are empty → 2 validation errors
        var result = await behavior.Handle(new TestCommand("", ""), next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Details.Should().NotBeNull();
        result.Error.Details!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Handle_FailureDetails_ShouldContainFieldName()
    {
        var validators = new[] { new TestCommandValidator() };
        var behavior = new ValidationBehavior<TestCommand, Result<string>>(validators);

        RequestHandlerDelegate<Result<string>> next = () =>
            Task.FromResult(Result<string>.Success("ok"));

        var result = await behavior.Handle(new TestCommand("", "a@b.com"), next, CancellationToken.None);

        result.Error!.Details.Should().ContainSingle();
        var detail = result.Error.Details![0];
        detail.Message.Key.Should().Be("lockey_test_name_required");
        detail.Message.Params.Should().ContainKey("field");
        detail.Message.Params!["field"].Should().Be("Name");
    }

    [Fact]
    public async Task Handle_InvalidEmailFormat_ShouldReturnLockeyKey()
    {
        var validators = new[] { new TestCommandValidator() };
        var behavior = new ValidationBehavior<TestCommand, Result<string>>(validators);

        RequestHandlerDelegate<Result<string>> next = () =>
            Task.FromResult(Result<string>.Success("ok"));

        var result = await behavior.Handle(new TestCommand("valid", "not-email"), next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        var emailError = result.Error!.Details!.First(d => d.Message.Params!["field"] == "Email");
        emailError.Message.Key.Should().Be("lockey_test_email_format");
    }

    [Fact]
    public async Task Handle_NonResultResponse_InvalidRequest_ShouldThrowValidationException()
    {
        var validators = new[] { new TestNonResultCommandValidator() };
        var behavior = new ValidationBehavior<TestNonResultCommand, string>(validators);

        RequestHandlerDelegate<string> next = () => Task.FromResult("ok");

        var act = () => behavior.Handle(new TestNonResultCommand(""), next, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_NonLockeyErrorMessage_ShouldFallbackToGenericKey()
    {
        // Create a validator with a non-lockey message
        var validator = Substitute.For<IValidator<TestCommand>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<TestCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new FluentValidation.Results.ValidationResult(
            [
                new FluentValidation.Results.ValidationFailure("Name", "plain error message")
            ]));

        var behavior = new ValidationBehavior<TestCommand, Result<string>>([validator]);

        RequestHandlerDelegate<Result<string>> next = () =>
            Task.FromResult(Result<string>.Success("ok"));

        var result = await behavior.Handle(new TestCommand("x", "a@b.com"), next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        // Non-lockey messages should fallback to "lockey_validation_failed"
        result.Error!.Details![0].Message.Key.Should().Be("lockey_validation_failed");
    }
}
