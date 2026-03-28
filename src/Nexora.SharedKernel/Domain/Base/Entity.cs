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
public abstract class Entity<TId> : IHasDomainEvents, IEquatable<Entity<TId>> where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        // Strict type equality — all Nexora entities are sealed and proxies are not expected.
        if (GetType() != other.GetType())
            return false;

        // Treat null or default IDs as transient — transient entities are never equal
        if (Id is null || other.Id is null)
            return false;

        if (EqualityComparer<TId>.Default.Equals(Id, default!) ||
            EqualityComparer<TId>.Default.Equals(other.Id, default!))
            return false;

        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) =>
        Equals(obj as Entity<TId>);

    public override int GetHashCode() =>
        Id is null || EqualityComparer<TId>.Default.Equals(Id, default!)
            ? 0
            : Id.GetHashCode();

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) =>
        !(left == right);
}
