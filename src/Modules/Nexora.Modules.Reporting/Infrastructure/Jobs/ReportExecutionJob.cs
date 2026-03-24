using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure.Services;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;

namespace Nexora.Modules.Reporting.Infrastructure.Jobs;

public sealed record ReportExecutionJobParams : JobParams
{
    public required Guid ExecutionId { get; init; }
}

/// <summary>
/// Hangfire job that runs a queued report execution: fetch SQL, execute, export, upload to MinIO.
/// </summary>
public sealed class ReportExecutionJob(
    ITenantContextAccessor tenantContextAccessor,
    ReportingDbContext dbContext,
    ReportExecutionService executionService,
    ReportExportService exportService,
    IFileStorageService fileStorageService,
    ILogger<ReportExecutionJob> logger)
    : NexoraJob<ReportExecutionJobParams>(tenantContextAccessor, logger)
{
    protected override async Task ExecuteAsync(ReportExecutionJobParams parameters, CancellationToken ct)
    {
        var executionId = ReportExecutionId.From(parameters.ExecutionId);

        var execution = await dbContext.ReportExecutions
            .FirstOrDefaultAsync(e => e.Id == executionId, ct);

        if (execution is null)
        {
            logger.LogWarning("Report execution {ExecutionId} not found", parameters.ExecutionId);
            return;
        }

        var definition = await dbContext.ReportDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == execution.DefinitionId, ct);

        if (definition is null)
        {
            execution.MarkFailed("Report definition not found", 0);
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        execution.MarkRunning();
        await dbContext.SaveChangesAsync(ct);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Parse parameters if present
            Dictionary<string, object?>? queryParams = null;
            if (!string.IsNullOrEmpty(execution.ParameterValues))
            {
                queryParams = System.Text.Json.JsonSerializer
                    .Deserialize<Dictionary<string, object?>>(execution.ParameterValues);
            }

            // Execute SQL
            var rows = await executionService.ExecuteQueryAsync(
                parameters.TenantId, definition.QueryText, queryParams, ct);

            sw.Stop();

            // Export to format
            var formatStr = execution.Format.ToString();
            var bytes = exportService.Export(rows, formatStr, definition.Name);

            // Upload to MinIO
            var extension = ReportExportService.GetFileExtension(formatStr);
            var storageKey = $"reports/{parameters.TenantId}/{execution.Id.Value}{extension}";

            // Upload bytes directly via presigned URL pattern
            var uploadUrl = await fileStorageService.GenerateUploadPresignedUrlAsync(
                "nexora-reports", storageKey,
                ReportExportService.GetContentType(formatStr),
                TimeSpan.FromMinutes(5), ct);

            // Direct upload via HTTP
            using var httpClient = new HttpClient();
            using var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                ReportExportService.GetContentType(formatStr));
            await httpClient.PutAsync(uploadUrl.Url, content, ct);

            execution.MarkCompleted(storageKey, rows.Count, sw.ElapsedMilliseconds);

            logger.LogInformation(
                "Report execution {ExecutionId} completed: {RowCount} rows in {DurationMs}ms",
                execution.Id, rows.Count, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            execution.MarkFailed(ex.Message, sw.ElapsedMilliseconds);
            logger.LogError(ex, "Report execution {ExecutionId} failed", execution.Id);
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
