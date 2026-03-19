using MediatR;
using Nexora.SharedKernel.Results;

namespace Nexora.SharedKernel.Abstractions.CQRS;

/// <summary>
/// Marker for a command that returns Result.
/// </summary>
public interface ICommand : IRequest<Result>
{
}

/// <summary>
/// Marker for a command that returns Result&lt;T&gt;.
/// </summary>
public interface ICommand<T> : IRequest<Result<T>>
{
}

/// <summary>
/// Handler for a command that returns Result.
/// </summary>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand
{
}

/// <summary>
/// Handler for a command that returns Result&lt;T&gt;.
/// </summary>
public interface ICommandHandler<in TCommand, T> : IRequestHandler<TCommand, Result<T>>
    where TCommand : ICommand<T>
{
}
