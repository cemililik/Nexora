using Nexora.Modules.Reporting.Domain.Events;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Reporting.Domain.Entities;

/// <summary>
/// Tracks a single execution of a <see cref="ReportDefinition"/>. The
/// lifecycle is <c>Queued → Running → Completed</c> on success and
/// <c>Queued|Running → Failed</c> on error. <see cref="MarkRunning"/> is
/// deliberately idempotent across Hangfire retries (Queued/Running/Failed
/// all transition to Running); only <see cref="ReportStatus.Completed"/>
/// is terminal because the result file + outbox event have already shipped
/// and re-running would duplicate them.
/// </summary>
public sealed class ReportExecution : AuditableEntity<ReportExecutionId>, IAggregateRoot
{
    /// <summary>
    /// Tenant that owns this execution. Stored as a raw <see cref="Guid"/>
    /// to match the convention used by every other domain entity in the
    /// platform (Contacts, Notifications, Documents, etc.) — tenant id is
    /// a cross-cutting identifier flowing through middleware, JWT, and
    /// schema-per-tenant routing where a strongly-typed wrapper would
    /// friction every boundary. Strongly-typed IDs (per CLAUDE.md) cover
    /// entity-owned identifiers like <see cref="ReportExecutionId"/>.
    /// </summary>
    public Guid TenantId { get; private set; }

    /// <summary>The report definition being executed.</summary>
    public ReportDefinitionId DefinitionId { get; private set; }

    /// <summary>Current lifecycle status — see class summary for the state machine.</summary>
    public ReportStatus Status { get; private set; }

    /// <summary>JSON-serialised parameter map supplied at queue time. <c>null</c> when the report has no parameters.</summary>
    public string? ParameterValues { get; private set; }

    /// <summary>MinIO object key of the generated result file. Set when transitioning to <see cref="ReportStatus.Completed"/>.</summary>
    public string? ResultStorageKey { get; private set; }

    /// <summary>Output format chosen at queue time (Csv, Xlsx, Pdf, …).</summary>
    public ReportFormat Format { get; private set; }

    /// <summary>Number of rows the query produced. Set on Completed; <c>null</c> while Queued/Running and on Failed.</summary>
    public int? RowCount { get; private set; }

    /// <summary>Wall-clock duration of the run in milliseconds. Set on Completed and on Failed.</summary>
    public long? DurationMs { get; private set; }

    /// <summary>Last failure message (lockey or freeform) — set on Failed; cleared by <see cref="MarkRunning"/> when resurrecting from Failed.</summary>
    public string? ErrorDetails { get; private set; }

    /// <summary>User principal that triggered the execution. Null for scheduled/system runs.</summary>
    public string? ExecutedBy { get; private set; }

    /// <summary>Hangfire background-job id assigned at enqueue time. Persisted across retries so operators can correlate.</summary>
    public string? HangfireJobId { get; private set; }

    private ReportExecution() { }

    /// <summary>
    /// Creates a new <see cref="ReportExecution"/> in the
    /// <see cref="ReportStatus.Queued"/> state. Use this factory rather
    /// than the parameterless constructor (which exists only for EF Core
    /// rehydration).
    /// </summary>
    /// <param name="tenantId">Tenant scope of the execution.</param>
    /// <param name="definitionId">Report definition being executed.</param>
    /// <param name="format">Output format requested by the caller.</param>
    /// <param name="parameterValues">JSON-serialised parameter map; <c>null</c> if the report has no parameters.</param>
    /// <param name="executedBy">User principal that triggered the execution; <c>null</c> for scheduled/system runs.</param>
    /// <returns>A new execution row with a fresh <see cref="ReportExecutionId"/>.</returns>
    public static ReportExecution Create(
        Guid tenantId,
        ReportDefinitionId definitionId,
        ReportFormat format,
        string? parameterValues,
        string? executedBy)
    {
        return new ReportExecution
        {
            Id = ReportExecutionId.New(),
            TenantId = tenantId,
            DefinitionId = definitionId,
            Status = ReportStatus.Queued,
            Format = format,
            ParameterValues = parameterValues,
            ExecutedBy = executedBy
        };
    }

    /// <summary>
    /// Transitions the execution into the <see cref="ReportStatus.Running"/>
    /// state. Idempotent across Hangfire retries — see the class summary
    /// for the full state machine.
    /// </summary>
    /// <param name="hangfireJobId">
    /// Hangfire job id to record. When <c>null</c> (the common case for
    /// retry callers that don't re-enqueue), the existing
    /// <see cref="HangfireJobId"/> is preserved instead of being wiped to
    /// <c>null</c> — that lets operators trace a retried run back to the
    /// original Hangfire id.
    /// </param>
    /// <exception cref="DomainException">
    /// Thrown when <see cref="Status"/> is already
    /// <see cref="ReportStatus.Completed"/>; a completed run has shipped
    /// its result file and outbox event and must not be resumed.
    /// </exception>
    public void MarkRunning(string? hangfireJobId = null)
    {
        // Idempotent across Hangfire retries. Valid starting states:
        //   Queued  — first attempt: normal initial transition.
        //   Running — previous attempt crashed between MarkRunning's
        //             SaveChanges and MarkCompleted / MarkFailed.
        //             Re-asserting Running is a safe no-op (status assign
        //             below is identity).
        //   Failed  — previous attempt called MarkFailed in its catch
        //             before rethrowing; Hangfire's retry reloads the
        //             execution and must be allowed to re-run. Transition
        //             Failed → Running so MarkCompleted on this attempt
        //             can finalise. ErrorDetails is cleared so a successful
        //             retry does not surface a stale error message in the
        //             admin UI's execution list.
        // Only Completed is terminal — an already-Completed execution must
        // not be resumed.
        if (Status == ReportStatus.Completed)
            throw new DomainException("lockey_reporting_error_execution_not_queued");

        // Resurrecting a Failed execution: clear the previous attempt's
        // error so the retry's outcome is the only one visible.
        if (Status == ReportStatus.Failed)
            ErrorDetails = null;

        Status = ReportStatus.Running;

        // Only overwrite HangfireJobId when the caller actually supplied
        // one. ExecuteAsync's retry path calls MarkRunning() with no
        // argument and would otherwise wipe the original enqueue id,
        // breaking operator-facing job correlation.
        if (hangfireJobId is not null)
        {
            HangfireJobId = hangfireJobId;
        }
    }

    /// <summary>
    /// Marks the execution as <see cref="ReportStatus.Completed"/> and
    /// records the produced artifact's storage key, row count, and total
    /// duration. Raises <see cref="ReportExecutionCompletedEvent"/> for
    /// downstream notification dispatch.
    /// </summary>
    /// <param name="resultStorageKey">MinIO object key of the uploaded result file.</param>
    /// <param name="rowCount">Number of rows produced by the underlying query.</param>
    /// <param name="durationMs">Wall-clock duration of the run in milliseconds.</param>
    /// <exception cref="DomainException">
    /// Thrown when <see cref="Status"/> is not <see cref="ReportStatus.Running"/>.
    /// </exception>
    public void MarkCompleted(string resultStorageKey, int rowCount, long durationMs)
    {
        if (Status != ReportStatus.Running)
            throw new DomainException("lockey_reporting_error_execution_not_running");
        Status = ReportStatus.Completed;
        ResultStorageKey = resultStorageKey;
        RowCount = rowCount;
        DurationMs = durationMs;
        AddDomainEvent(new ReportExecutionCompletedEvent(Id, DefinitionId));
    }

    /// <summary>
    /// Marks the execution as <see cref="ReportStatus.Failed"/> with the
    /// supplied error details. Callable from either the
    /// <see cref="ReportStatus.Queued"/> or <see cref="ReportStatus.Running"/>
    /// state — not from a terminal state.
    /// </summary>
    /// <param name="errorDetails">Lockey or free-form error description; surfaced in the admin UI execution list.</param>
    /// <param name="durationMs">Wall-clock duration of the failed run in milliseconds.</param>
    /// <exception cref="DomainException">
    /// Thrown when <see cref="Status"/> is already
    /// <see cref="ReportStatus.Completed"/> or <see cref="ReportStatus.Failed"/>
    /// — those are terminal states and a second outcome write would be
    /// ambiguous.
    /// </exception>
    public void MarkFailed(string errorDetails, long durationMs)
    {
        if (Status is not (ReportStatus.Queued or ReportStatus.Running))
            throw new DomainException("lockey_reporting_error_execution_already_finished");
        Status = ReportStatus.Failed;
        ErrorDetails = errorDetails;
        DurationMs = durationMs;
    }
}
