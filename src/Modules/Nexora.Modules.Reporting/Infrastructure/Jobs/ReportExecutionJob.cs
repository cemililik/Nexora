using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Application.Services;
using Nexora.Modules.Reporting.Infrastructure.Services;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Reporting.Infrastructure.Jobs;

/// <summary>Parameters for the report execution Hangfire job.</summary>
public sealed record ReportExecutionJobParams : JobParams
{
    /// <summary>Gets the ID of the report execution to process.</summary>
    public required Guid ExecutionId { get; init; }
}

/// <summary>
/// Hangfire job that runs a queued report execution: fetch SQL, execute, export, upload to MinIO.
/// </summary>
public sealed class ReportExecutionJob(
    ITenantContextAccessor tenantContextAccessor,
    ReportingDbContext dbContext,
    IReportExecutionService executionService,
    ReportExportService exportService,
    IFileStorageService fileStorageService,
    IOutbox outbox,
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
            execution.MarkFailed("lockey_reporting_error_definition_not_found", 0);
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        execution.MarkRunning();
        await dbContext.SaveChangesAsync(ct);

        var sw = Stopwatch.StartNew();

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
            using var exportStream = exportService.Export(rows, formatStr, definition.Name);

            // Upload to MinIO
            var extension = ReportExportService.GetFileExtension(formatStr);
            var storageKey = $"reports/{parameters.TenantId}/{execution.Id.Value}{extension}";

            await fileStorageService.UploadObjectAsync(
                "nexora-reports", storageKey, exportStream,
                ReportExportService.GetContentType(formatStr), ct);

            execution.MarkCompleted(storageKey, rows.Count, sw.ElapsedMilliseconds);

            await outbox.EnqueueAsync(new ReportExecutedIntegrationEvent
            {
                TenantId = parameters.TenantId,
                ExecutionId = execution.Id.Value,
                DefinitionId = definition.Id.Value,
                ReportName = definition.Name,
                DurationMs = sw.ElapsedMilliseconds
            }, ct);

            logger.LogInformation(
                "Report execution {ExecutionId} completed: {RowCount} rows in {DurationMs}ms",
                execution.Id, rows.Count, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            execution.MarkFailed("lockey_reporting_error_execution_failed", sw.ElapsedMilliseconds);
            // Use CancellationToken.None so a late cancellation signal does not
            // prevent persisting the failure state before re-throwing.
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw; // NexoraJob base class handles logging and telemetry
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
