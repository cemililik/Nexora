using MediatR;

namespace Nexora.SharedKernel.Domain.Events;

/// <summary>
/// Domain event — dispatched within the same bounded context after SaveChanges.
/// </summary>
public interface IDomainEvent : INotification
{
    DateTime OccurredAt { get; }
}

public abstract record DomainEventBase : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
