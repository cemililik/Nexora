using MediatR;

namespace Nexora.SharedKernel.Domain.Events;

/// <summary>
/// Domain event — dispatched within the same bounded context after SaveChanges.
/// </summary>
public interface IDomainEvent : INotification
{
    /// <summary>The UTC timestamp when the event occurred.</summary>
    DateTime OccurredAt { get; }
}

/// <summary>
/// Base record for domain events with automatic timestamp.
/// </summary>
public abstract record DomainEventBase : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
