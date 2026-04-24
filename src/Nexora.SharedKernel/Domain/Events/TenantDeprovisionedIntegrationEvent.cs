namespace Nexora.SharedKernel.Domain.Events;

/// <summary>
/// Published when a tenant's schema has been fully dropped from the platform
/// — typically via <c>nexora demo:clean --drop-tenant</c> (T-009) or an NMP
/// tenant-deprovision operation. Downstream consumers clean up tenant-scoped
/// external state: MinIO bucket contents, Keycloak realm, cache entries, CDN
/// prefixes, third-party integration webhooks.
///
/// <para>
/// The schema CASCADE drop in the emitter is atomic, but this event is the
/// <b>only</b> signal to non-database systems that the tenant is gone —
/// there is no periodic reconciliation job by design (those systems'
/// footprints are small and operators should not have to wait for a sweep
/// to reclaim storage). Consumers MUST be idempotent: a redelivery of the
/// same <see cref="IIntegrationEvent.EventId"/> against an already-cleaned
/// external system is a no-op.
/// </para>
/// </summary>
public sealed record TenantDeprovisionedIntegrationEvent : IntegrationEventBase
{
    /// <summary>
    /// Schema name that was dropped — always of the form <c>tenant_{guid}</c>.
    /// Included alongside <see cref="IIntegrationEvent.TenantId"/> so consumers
    /// that key external resources by schema name (e.g. MinIO prefix) do not
    /// need to rebuild it.
    /// </summary>
    public required string SchemaName { get; init; }

    /// <summary>UTC timestamp when the schema was dropped.</summary>
    public required DateTime DeprovisionedAtUtc { get; init; }
}
