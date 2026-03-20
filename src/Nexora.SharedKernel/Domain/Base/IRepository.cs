namespace Nexora.SharedKernel.Domain.Base;

/// <summary>
/// Generic repository contract for aggregate roots.
/// </summary>
public interface IRepository<TEntity, TId>
    where TEntity : Entity<TId>, IAggregateRoot
    where TId : notnull
{
    /// <summary>Gets an entity by its identifier, or null if not found.</summary>
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default);

    /// <summary>Adds a new entity to the repository.</summary>
    Task AddAsync(TEntity entity, CancellationToken ct = default);

    /// <summary>Marks an existing entity as modified.</summary>
    void Update(TEntity entity);

    /// <summary>Marks an entity for removal.</summary>
    void Remove(TEntity entity);
}
