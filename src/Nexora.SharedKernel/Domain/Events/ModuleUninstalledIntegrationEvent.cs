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
    /// uninstall time. May be empty when the database provider doesn't
    /// support introspection (e.g. EF InMemory in tests).
    /// </summary>
    public IReadOnlyList<string> CanonicalTableNames { get; init; } = [];

    /// <summary>
    /// Renamed-to-<c>_del_</c> table names in the tenant schema. One-to-one
    /// with <see cref="CanonicalTableNames"/> in the same order.
    /// </summary>
    public IReadOnlyList<string> RenamedTableNames { get; init; } = [];

    /// <summary>UTC timestamp at which the per-module transaction committed.</summary>
    public DateTimeOffset UninstalledAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
