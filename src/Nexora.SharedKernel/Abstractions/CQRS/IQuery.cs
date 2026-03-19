using MediatR;
using Nexora.SharedKernel.Results;

namespace Nexora.SharedKernel.Abstractions.CQRS;

/// <summary>
/// Marker for a query that returns Result&lt;T&gt;.
/// </summary>
public interface IQuery<T> : IRequest<Result<T>>
{
}

/// <summary>
/// Handler for a query that returns Result&lt;T&gt;.
/// </summary>
public interface IQueryHandler<in TQuery, T> : IRequestHandler<TQuery, Result<T>>
    where TQuery : IQuery<T>
{
}
