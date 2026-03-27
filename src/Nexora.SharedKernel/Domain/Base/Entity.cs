using Nexora.SharedKernel.Domain.Events;

namespace Nexora.SharedKernel.Domain.Base;

/// <summary>
/// Contract for entities that raise domain events.
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>Gets the list of uncommitted domain events.</summary>
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>Clears all pending domain events.</summary>
    void ClearDomainEvents();
}

/// <summary>
/// Base entity with identity and domain event support.
/// </summary>
public abstract class Entity<TId> : IHasDomainEvents where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (Id is null || other.Id is null)
            return false;

        return Id.Equals(other.Id);
    }

    public override int GetHashCode() =>
        Id is null ? 0 : Id.GetHashCode();
}
