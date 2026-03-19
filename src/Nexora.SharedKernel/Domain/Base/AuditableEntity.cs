namespace Nexora.SharedKernel.Domain.Base;

/// <summary>
/// Entity with audit trail fields. Set automatically by BaseDbContext.
/// </summary>
public abstract class AuditableEntity<TId> : Entity<TId> where TId : notnull
{
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

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
}
