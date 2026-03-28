namespace Nexora.SharedKernel.Domain.Base;

/// <summary>
/// Entity with audit trail and soft delete fields. Set automatically by BaseDbContext.
/// </summary>
public abstract class AuditableEntity<TId> : Entity<TId>, ISoftDeletable where TId : notnull
{
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    /// <summary>Whether this entity has been soft-deleted.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>When the entity was soft-deleted.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Who soft-deleted the entity.</summary>
    public string? DeletedBy { get; private set; }

    internal void SetCreated(DateTimeOffset at, string? by)
    {
        CreatedAt = at;
        CreatedBy = by;
    }

    internal void SetUpdated(DateTimeOffset at, string? by)
    {
        UpdatedAt = at;
        UpdatedBy = by;
    }

    /// <summary>Marks entity as soft-deleted. Prefer using dbContext.Remove() which does this automatically.</summary>
    public void MarkAsDeleted(DateTimeOffset at, string? by)
    {
        if (at == default)
            throw new ArgumentException("Deletion timestamp must be provided.", nameof(at));

        IsDeleted = true;
        DeletedAt = at;
        DeletedBy = by;
    }

    /// <summary>Reverses a soft-delete, making the entity visible again.</summary>
    public void UndoDelete()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }
}
