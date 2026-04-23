using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Contacts.Domain.Entities;

/// <summary>
/// Tracks the status and progress of a contact export job.
/// Maps a client-facing JobId to a Hangfire background job.
/// </summary>
public sealed class ExportJob : Entity<ExportJobId>
{
    /// <summary>Tenant that owns this export.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Organization scope for this export.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Output file format (csv | xlsx | vcard).</summary>
    public string Format { get; private set; } = default!;

    /// <summary>MinIO storage key once the file has been uploaded. Null while Queued/Processing/Failed.</summary>
    public string? StorageKey { get; private set; }

    /// <summary>Current status of the export job.</summary>
    public ExportJobStatus Status { get; private set; }

    /// <summary>Number of contacts exported.</summary>
    public int TotalRows { get; private set; }

    /// <summary>Error details (lockey or message) when the job fails.</summary>
    public string? ErrorDetails { get; private set; }

    /// <summary>Hangfire job id for this export.</summary>
    public string? HangfireJobId { get; private set; }

    /// <summary>Serialized filter parameters (status/type/date range) captured for retry idempotency.</summary>
    public string? FiltersJson { get; private set; }

    /// <summary>Serialized selected fields (core field names + custom field ids) for retry idempotency.</summary>
    public string? FieldsJson { get; private set; }

    /// <summary>User who initiated the export.</summary>
    public string? CreatedBy { get; private set; }

    /// <summary>UTC timestamp when the job record was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>UTC timestamp when the job completed (success or failure).</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    private ExportJob() { }

    /// <summary>Creates a new export job in Queued status.</summary>
    public static ExportJob Create(
        Guid tenantId,
        Guid organizationId,
        string format,
        string? filtersJson,
        string? fieldsJson,
        string? createdBy)
    {
        if (string.IsNullOrWhiteSpace(format))
            throw new DomainException("lockey_contacts_validation_export_format_required");

        return new ExportJob
        {
            Id = ExportJobId.New(),
            TenantId = tenantId,
            OrganizationId = organizationId,
            Format = format.ToLowerInvariant(),
            FiltersJson = filtersJson,
            FieldsJson = fieldsJson,
            Status = ExportJobStatus.Queued,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Records the Hangfire job ID after enqueue.</summary>
    public void SetHangfireJobId(string hangfireJobId)
    {
        if (string.IsNullOrWhiteSpace(hangfireJobId))
            throw new DomainException("lockey_contacts_error_export_hangfire_id_required");

        HangfireJobId = hangfireJobId;
    }

    /// <summary>Transitions the job to Processing status with the planned row count.</summary>
    public void MarkProcessing(int totalRows)
    {
        if (Status != ExportJobStatus.Queued)
            throw new InvalidOperationException(
                $"Cannot transition to Processing from {Status}. Expected: Queued.");

        if (totalRows < 0)
            throw new ArgumentOutOfRangeException(nameof(totalRows), "TotalRows must be non-negative.");

        Status = ExportJobStatus.Processing;
        TotalRows = totalRows;
    }

    /// <summary>
    /// Refreshes the planned row count while the job is in <see cref="ExportJobStatus.Processing"/>.
    /// Used by job resume paths so the outbox payload and completion notification report the
    /// latest query result (e.g. when concurrent writes have added / removed rows since the
    /// original MarkProcessing transition).
    /// </summary>
    public void UpdateTotalRows(int totalRows)
    {
        if (Status != ExportJobStatus.Processing)
            throw new DomainException("lockey_contacts_error_export_update_total_rows_invalid_status");
        if (totalRows < 0)
            throw new DomainException("lockey_contacts_error_export_total_rows_negative");

        TotalRows = totalRows;
    }

    /// <summary>Marks the job as completed and records the storage key of the generated file.</summary>
    public void MarkCompleted(string storageKey)
    {
        if (Status != ExportJobStatus.Processing)
            throw new InvalidOperationException(
                $"Cannot transition to Completed from {Status}. Expected: Processing.");

        if (string.IsNullOrWhiteSpace(storageKey))
            throw new DomainException("lockey_contacts_error_export_storage_key_required");

        Status = ExportJobStatus.Completed;
        StorageKey = storageKey;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Marks the job as failed with an error lockey or message.</summary>
    public void MarkFailed(string errorKey)
    {
        if (Status is ExportJobStatus.Completed or ExportJobStatus.Failed)
            throw new InvalidOperationException(
                $"Cannot transition to Failed from {Status}.");

        Status = ExportJobStatus.Failed;
        ErrorDetails = errorKey;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
