namespace Nexora.SharedKernel.Domain.Base;

/// <summary>
/// Marker interface for aggregate roots.
/// Only aggregate roots can be persisted via repositories.
/// </summary>
public interface IAggregateRoot;
