namespace Nexora.SharedKernel.Domain.Events;

/// <summary>Published when a module is uninstalled for a tenant.</summary>
/// <remarks>
/// T-026: extended schema per ADR-0028 — the event now carries the canonical
/// table list (the names the module owned at uninstall time) and the renamed
/// list (the actual <c>_del_</c>-suffixed tables in the tenant schema).
/// Consumers like the cleanup job (T-025) and the GDPR escape hatch (T-027)
/// MUST work off these fields rather than reconstructing names from the
/// module manifest, because schema-evolved tables added by
/// <c>ApplySchemaUpdatesAsync</c> would otherwise be missed.
/// </remarks>
public sealed record ModuleUninstalledIntegrationEvent : IntegrationEventBase
{
    /// <summary>Gets the name of the uninstalled module.</summary>
    public required string ModuleName { get; init; }

    /// <summary>Gets the tenant identifier as a Guid.</summary>
    public required Guid TenantIdGuid { get; init; }

    /// <summary>
    /// Canonical (pre-rename) table names that belonged to the module at
    /// uninstall time. Empty list is valid when the database provider
    /// doesn't support introspection (EF InMemory in tests) — the
    /// invariant the consumer must rely on is "same length as
    /// <see cref="RenamedTableNames"/>". Required so producers cannot
    /// forget to populate it.
    /// </summary>
    public required IReadOnlyList<string> CanonicalTableNames { get; init; }

    /// <summary>
    /// Renamed-to-<c>_del_</c> table names in the tenant schema. One-to-one
    /// with <see cref="CanonicalTableNames"/> in the same order — the
    /// producer guarantees this; consumers SHOULD assert
    /// <c>Canonical.Count == Renamed.Count</c> and treat any divergence
    /// as a producer bug.
    /// </summary>
    public required IReadOnlyList<string> RenamedTableNames { get; init; }

    // NOTE: <c>UninstalledAtUtc</c> intentionally absent — the
    // <see cref="IntegrationEventBase.OccurredAt"/> field on the base
    // record already carries the per-event UTC timestamp; a duplicate
    // field on the payload would diverge under cascade orchestration
    // (one event raised in a loop, "now" called twice). Consumers read
    // <c>OccurredAt</c>.
}
