using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Contacts.Domain.Entities;

/// <summary>
/// Tracks the status and progress of a contact import job.
/// Maps a client-facing JobId to a Hangfire background job.
/// </summary>
public sealed class ImportJob : Entity<ImportJobId>
{
    public Guid TenantId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string FileName { get; private set; } = default!;
    public string FileFormat { get; private set; } = default!;
    public string StorageKey { get; private set; } = default!;
    public ImportJobStatus Status { get; private set; }
    public int TotalRows { get; private set; }
    public int ProcessedRows { get; private set; }
    public int SuccessCount { get; private set; }
    public int ErrorCount { get; private set; }
    public string? ErrorDetails { get; private set; }
    public string? HangfireJobId { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private ImportJob() { }

    /// <summary>Creates a new import job in Queued status.</summary>
    public static ImportJob Create(
        Guid tenantId,
        Guid organizationId,
        string fileName,
        string fileFormat,
        string storageKey,
        string? createdBy)
    {
        return new ImportJob
        {
            Id = ImportJobId.New(),
            TenantId = tenantId,
            OrganizationId = organizationId,
            FileName = fileName,
            FileFormat = fileFormat,
            StorageKey = storageKey,
            Status = ImportJobStatus.Queued,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Records the Hangfire job ID after enqueue.</summary>
    public void SetHangfireJobId(string hangfireJobId)
    {
        if (string.IsNullOrWhiteSpace(hangfireJobId))
            throw new DomainException("lockey_contacts_error_import_hangfire_id_required");

        HangfireJobId = hangfireJobId;
    }

    /// <summary>Transitions the job to Processing status with the total row count.</summary>
    public void MarkProcessing(int totalRows)
    {
        Status = ImportJobStatus.Processing;
        TotalRows = totalRows;
    }

    /// <summary>Updates the progress counters during processing.</summary>
    public void UpdateProgress(int processedRows, int successCount, int errorCount)
    {
        ProcessedRows = processedRows;
        SuccessCount = successCount;
        ErrorCount = errorCount;
    }

    /// <summary>Marks the job as successfully completed.</summary>
    public void MarkCompleted()
    {
        Status = ImportJobStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Marks the job as failed with error details.</summary>
    public void MarkFailed(string errorDetails)
    {
        Status = ImportJobStatus.Failed;
        ErrorDetails = errorDetails;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
