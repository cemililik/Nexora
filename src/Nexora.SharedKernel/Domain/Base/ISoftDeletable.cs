namespace Nexora.SharedKernel.Domain.Base;

/// <summary>
/// Marker interface for entities that support soft deletion.
/// Soft-deleted entities are excluded from queries via global query filters.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>Whether this entity has been soft-deleted.</summary>
    bool IsDeleted { get; }

    /// <summary>When the entity was soft-deleted.</summary>
    DateTimeOffset? DeletedAt { get; }

    /// <summary>Who soft-deleted the entity.</summary>
    string? DeletedBy { get; }
}
