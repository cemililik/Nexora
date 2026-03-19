using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Infrastructure.Behaviors;
using NSubstitute;

namespace Nexora.Infrastructure.Tests.Behaviors;

public sealed record TestQuery(int Id) : IRequest<string>;

public sealed class LoggingBehaviorTests
{
    [Fact]
    public async Task Handle_ShouldCallNextAndReturnResult()
    {
        var logger = Substitute.For<ILogger<LoggingBehavior<TestQuery, string>>>();
        var behavior = new LoggingBehavior<TestQuery, string>(logger);

        RequestHandlerDelegate<string> next = () => Task.FromResult("result");

        var result = await behavior.Handle(new TestQuery(1), next, CancellationToken.None);

        result.Should().Be("result");
    }

    [Fact]
    public async Task Handle_ShouldLogRequestName()
    {
        var logger = Substitute.For<ILogger<LoggingBehavior<TestQuery, string>>>();
        var behavior = new LoggingBehavior<TestQuery, string>(logger);

        RequestHandlerDelegate<string> next = () => Task.FromResult("result");

        await behavior.Handle(new TestQuery(1), next, CancellationToken.None);

        logger.ReceivedCalls().Should().NotBeEmpty();
    }
}
