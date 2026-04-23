namespace Nexora.Infrastructure.Modules;

/// <summary>
/// Idempotency marker for per-(tenant, module, scenario) demo-data seeding (T-005).
/// One row is written when <c>IModule.SeedDemoDataAsync</c> finishes successfully;
/// subsequent runs with the same <c>(TenantId, ModuleName, Scenario)</c> short-circuit.
/// </summary>
public sealed class DemoSeedMarker
{
    public required Guid TenantId { get; init; }
    public required string ModuleName { get; init; }
    public required string Scenario { get; init; }
    public required DateTimeOffset SeededAt { get; init; }
}
