using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Nexora.Infrastructure.Behaviors;
using Nexora.SharedKernel.Results;
using NSubstitute;

namespace Nexora.Infrastructure.Tests.Behaviors;

// Test request/response types
public sealed record TestCommand(string Name) : IRequest<Result<string>>;

public sealed class TestCommandValidator : AbstractValidator<TestCommand>
{
    public TestCommandValidator()
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

        await behavior.Handle(new TestCommand("test"), next, CancellationToken.None);

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

        await behavior.Handle(new TestCommand("valid-name"), next, CancellationToken.None);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_InvalidRequest_ShouldNotCallNext()
    {
        var validators = new[] { new TestCommandValidator() };
        var behavior = new ValidationBehavior<TestCommand, Result<string>>(validators);

        var nextCalled = false;
        RequestHandlerDelegate<Result<string>> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result<string>.Success("ok"));
        };

        // Empty name should fail validation
        try
        {
            await behavior.Handle(new TestCommand(""), next, CancellationToken.None);
        }
        catch (ValidationException)
        {
            // Expected if Result<T> path doesn't match
        }

        nextCalled.Should().BeFalse();
    }
}
