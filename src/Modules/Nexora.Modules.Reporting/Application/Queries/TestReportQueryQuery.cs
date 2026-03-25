using Microsoft.Extensions.Logging;
using Nexora.Modules.Reporting.Application.Services;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Application.Queries;

/// <summary>Query to test-execute a SQL report query and return a limited preview of results.</summary>
public sealed record TestReportQueryQuery(string QueryText) : IQuery<TestReportQueryResultDto>;

/// <summary>DTO containing the column names, preview rows, and row count from a test query execution.</summary>
public sealed record TestReportQueryResultDto(
    IReadOnlyList<string> Columns,
    IReadOnlyList<Dictionary<string, object?>> Rows,
    int RowCount);

/// <summary>Handles test-executing a SQL query with a limited row count for preview purposes.</summary>
public sealed class TestReportQueryHandler(
    ISqlQueryValidator sqlQueryValidator,
    IReportExecutionService executionService,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<TestReportQueryHandler> logger) : IQueryHandler<TestReportQueryQuery, TestReportQueryResultDto>
{
    private const int PreviewLimit = 10;

    public async Task<Result<TestReportQueryResultDto>> Handle(TestReportQueryQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!sqlQueryValidator.IsValid(request.QueryText, out var sqlError))
        {
            logger.LogWarning("Test query validation failed for tenant {TenantId}: {SqlError}", tenantContextAccessor.Current.TenantId, sqlError);
            return Result<TestReportQueryResultDto>.Failure(LocalizedMessage.Of(sqlError!));
        }

        var tenantId = tenantContextAccessor.Current.TenantId;

        // Wrap in a subquery with LIMIT to prevent large result sets
        var limitedQuery = $"SELECT * FROM ({request.QueryText.TrimEnd().TrimEnd(';')}) AS _preview LIMIT {PreviewLimit}";

        try
        {
            var rows = await executionService.ExecuteQueryAsync(tenantId, limitedQuery, null, ct);

            var columns = rows.Count > 0
                ? rows[0].Keys.ToList()
                : new List<string>();

            return Result<TestReportQueryResultDto>.Success(
                new TestReportQueryResultDto(columns, rows, rows.Count));
        }
        catch (Npgsql.PostgresException ex)
        {
            logger.LogError(ex, "Test query execution failed for tenant {TenantId}", tenantId);
            return Result<TestReportQueryResultDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_query_failed"));
        }
    }
}
