using System.Data;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Reporting.Application.Services;
using Npgsql;

namespace Nexora.Modules.Reporting.Infrastructure.Services;

/// <summary>
/// Executes report SQL queries against the tenant schema using Dapper.
/// Enforces read-only transactions and query timeouts.
/// </summary>
public sealed class ReportExecutionService(
    ISqlQueryValidator sqlQueryValidator,
    IConfiguration configuration,
    ILogger<ReportExecutionService> logger) : IReportExecutionService
{
    private const int QueryTimeoutSeconds = 30;

    /// <summary>Executes a read-only SQL query within the tenant's schema.</summary>
    public async Task<IReadOnlyList<Dictionary<string, object?>>> ExecuteQueryAsync(
        string tenantId,
        string queryText,
        Dictionary<string, object?>? parameters,
        CancellationToken ct)
    {
        if (!sqlQueryValidator.IsValid(queryText, out var validationError))
        {
            logger.LogWarning("SQL validation failed for tenant {TenantId}: {Error}", tenantId, validationError);
            throw new InvalidOperationException(validationError);
        }

        var connectionString = configuration.GetConnectionString("Default");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        // Set tenant schema
        var schemaName = $"tenant_{tenantId}";
        await connection.ExecuteAsync($"SET search_path TO '{schemaName}'");

        // Read-only transaction with timeout
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await connection.ExecuteAsync("SET TRANSACTION READ ONLY", transaction: transaction);

        var dynamicParams = new DynamicParameters();
        if (parameters is not null)
        {
            foreach (var (key, value) in parameters)
                dynamicParams.Add(key, value);
        }

        var command = new CommandDefinition(
            queryText, dynamicParams, transaction,
            commandTimeout: QueryTimeoutSeconds,
            cancellationToken: ct);

        var rows = await connection.QueryAsync(command);

        var result = rows.Select(row =>
        {
            var dict = (IDictionary<string, object>)row;
            return dict.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
        }).ToList();

        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Report query executed for tenant {TenantId}, returned {RowCount} rows",
            tenantId, result.Count);

        return result;
    }
}
