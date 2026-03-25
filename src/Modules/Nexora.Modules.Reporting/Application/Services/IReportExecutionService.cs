namespace Nexora.Modules.Reporting.Application.Services;

/// <summary>
/// Executes report SQL queries against the tenant's schema.
/// </summary>
public interface IReportExecutionService
{
    /// <summary>Executes a read-only SQL query within the tenant's schema and returns the result rows.</summary>
    Task<IReadOnlyList<Dictionary<string, object?>>> ExecuteQueryAsync(
        string tenantId,
        string queryText,
        Dictionary<string, object?>? parameters,
        CancellationToken ct);
}
