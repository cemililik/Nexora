using Nexora.Modules.Reporting.Infrastructure.Services;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Application.Queries;

public sealed record TestReportQueryQuery(string QueryText) : IQuery<TestReportQueryResultDto>;

public sealed record TestReportQueryResultDto(
    IReadOnlyList<string> Columns,
    IReadOnlyList<Dictionary<string, object?>> Rows,
    int RowCount);

public sealed class TestReportQueryHandler(
    ReportExecutionService executionService,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<TestReportQueryQuery, TestReportQueryResultDto>
{
    private const int PreviewLimit = 10;

    public async Task<Result<TestReportQueryResultDto>> Handle(TestReportQueryQuery request, CancellationToken ct)
    {
        if (!SqlQueryValidator.IsValid(request.QueryText, out var sqlError))
            return Result<TestReportQueryResultDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_invalid_query", new() { ["reason"] = sqlError! }));

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
        catch (Exception ex)
        {
            return Result<TestReportQueryResultDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_query_failed", new() { ["reason"] = ex.Message }));
        }
    }
}
