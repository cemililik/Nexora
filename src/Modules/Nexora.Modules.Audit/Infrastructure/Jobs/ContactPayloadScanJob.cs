using System.ComponentModel;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.SharedKernel.Abstractions.Audit;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using OperationType = Nexora.SharedKernel.Abstractions.Audit.OperationType;

namespace Nexora.Modules.Audit.Infrastructure.Jobs;

/// <summary>Parameters for the cross-module audit-payload PII scan (T-010).</summary>
public sealed record ContactPayloadScanJobParams : JobParams
{
    /// <summary>Erased contact whose references are being scrubbed from audit payloads.</summary>
    public required Guid ContactId { get; init; }
}

/// <summary>
/// T-010 (ADR-0026): cross-module GDPR Article 17 sweep over audit
/// payload columns. Fired by <c>ContactGdprDeletedIntegrationEventHandler</c>
/// after the indexed <c>(EntityType, EntityId)</c> path completes; iterates
/// every registered <see cref="IContactReferenceLocator"/> and applies
/// each module's redaction rules to its own audit rows. Decoupled into a
/// Hangfire job because a tenant with millions of audit rows can take
/// minutes — the inbox handler must return quickly to ack the broker
/// message.
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotency.</b> Locators MUST return either the redacted payload
/// (a different string) or <see langword="null"/> when nothing to do.
/// Re-running the scan on already-redacted payloads is a no-op because
/// the second pass returns null at every entry. The compliance summary
/// row IS appended on every run — operators read its <c>Metadata</c> to
/// confirm a sweep completed; the most-recent row is the canonical
/// record per <c>(tenant, contact)</c>.
/// </para>
/// <para>
/// <b>Module coverage.</b> Modules that store contact PII inside audit
/// JSON columns MUST register an <see cref="IContactReferenceLocator"/>;
/// modules that don't (e.g. only stamp <c>EntityId = contactId</c>) are
/// fine without one because the indexed-path handler already covers them.
/// See <c>docs/standards/audit-coverage.md</c> §3.
/// </para>
/// </remarks>
[Queue(JobQueues.Maintenance)]
[DisplayName("audit:contact-payload-scan")]
public sealed class ContactPayloadScanJob(
    ITenantContextAccessor tenantContextAccessor,
    AuditDbContext dbContext,
    IEnumerable<IContactReferenceLocator> locators,
    ILogger<ContactPayloadScanJob> logger)
    : NexoraJob<ContactPayloadScanJobParams>(tenantContextAccessor, logger)
{
    /// <summary>Per-batch SaveChangesAsync threshold — bounds memory footprint at ~BatchSize tracked entities.</summary>
    private const int BatchSize = 500;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(ContactPayloadScanJobParams parameters, CancellationToken ct)
    {
        var locatorList = locators.ToList();
        if (locatorList.Count == 0)
        {
            logger.LogDebug(
                "Contact payload scan: no IContactReferenceLocator registered; sweep is a no-op for tenant {TenantId} contact {ContactId}.",
                parameters.TenantId, parameters.ContactId);
            await AppendSummaryAuditEntryAsync(parameters, perModuleStats: new Dictionary<string, int>(), ct);
            return;
        }

        var perModuleStats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var totalRedacted = 0;

        // One transaction wraps every per-module redaction batch + the
        // gdpr_erasure_scan summary so a tenant either ends the run with
        // (all redactions committed AND a summary row) or with (no
        // mutation at all). Without this, a SaveChanges after the first
        // module + a crash before the summary would leave the audit table
        // in a state where the redactions happened but the compliance
        // record proving they happened is missing — operators couldn't
        // tell the rows had already been processed. EF InMemory in tests
        // returns null from BeginTransactionAsync; we tolerate that.
        IDbContextTransaction? tx = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(ct)
            : null;

        try
        {
            foreach (var locator in locatorList)
            {
                ct.ThrowIfCancellationRequested();
                var redactedThisModule = await ScanModuleAsync(locator, parameters, ct);
                if (redactedThisModule > 0)
                    perModuleStats[locator.ModuleName] = redactedThisModule;
                totalRedacted += redactedThisModule;
            }

            await AppendSummaryAuditEntryAsync(parameters, perModuleStats, ct);

            if (tx is not null) await tx.CommitAsync(ct);
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }

        logger.LogInformation(
            "Contact payload scan completed: tenant {TenantId} contact {ContactId}; {TotalRedacted} entries redacted across {ModuleCount} module(s).",
            parameters.TenantId, parameters.ContactId, totalRedacted, perModuleStats.Count);
    }

    /// <summary>
    /// Streams this module's audit rows in <see cref="BatchSize"/>-sized
    /// pages and applies the locator's redaction in-place. Bounded memory:
    /// ~BatchSize tracked entities per page, regardless of the module's
    /// total row count. Each batch's mutations are flushed via
    /// <c>SaveChangesAsync</c> inside the outer transaction; the
    /// ChangeTracker is cleared between pages so EF doesn't accumulate
    /// references to entities the locator examined but did not mutate.
    /// </summary>
    private async Task<int> ScanModuleAsync(
        IContactReferenceLocator locator,
        ContactPayloadScanJobParams parameters,
        CancellationToken ct)
    {
        var redactedThisModule = 0;
        var batch = new List<Domain.Entities.AuditEntry>(BatchSize);
        var pendingChanges = 0;

        // Stable iteration order matters in production so partition
        // boundaries don't drop or duplicate rows mid-stream — under EF +
        // Npgsql we add ORDER BY "Timestamp" via a server-side sort. The
        // InMemory provider used in tests, however, materializes the
        // OrderBy through LINQ-to-Objects with the non-generic Comparer
        // (which fails on `DateTimeOffset` despite it implementing
        // IComparable), so we suppress the OrderBy when the provider is
        // not relational. Tests assert on row count + content, not order.
        IQueryable<Domain.Entities.AuditEntry> baseQuery = dbContext.AuditEntries
            .Where(e => e.TenantId == parameters.TenantId && e.Module == locator.ModuleName);
        if (dbContext.Database.IsRelational())
            baseQuery = baseQuery.OrderBy(e => e.Timestamp);
        var query = baseQuery.AsAsyncEnumerable();

        await foreach (var entry in query.WithCancellation(ct))
        {
            batch.Add(entry);
            var redactedBefore = locator.Redact(entry.BeforeState, parameters.ContactId);
            var redactedAfter = locator.Redact(entry.AfterState, parameters.ContactId);
            var redactedChanges = locator.Redact(entry.Changes, parameters.ContactId);

            if (redactedBefore is not null || redactedAfter is not null || redactedChanges is not null)
            {
                entry.ApplyLocatorRedaction(redactedBefore, redactedAfter, redactedChanges);
                redactedThisModule++;
                pendingChanges++;
            }

            if (batch.Count >= BatchSize)
            {
                if (pendingChanges > 0)
                {
                    await dbContext.SaveChangesAsync(ct);
                    pendingChanges = 0;
                }
                dbContext.ChangeTracker.Clear();
                batch.Clear();
            }
        }

        if (pendingChanges > 0)
        {
            await dbContext.SaveChangesAsync(ct);
        }

        return redactedThisModule;
    }

    private async Task AppendSummaryAuditEntryAsync(
        ContactPayloadScanJobParams parameters,
        IReadOnlyDictionary<string, int> perModuleStats,
        CancellationToken ct)
    {
        var metadata = JsonSerializer.Serialize(new
        {
            contactId = parameters.ContactId.ToString("D"),
            perModuleRedacted = perModuleStats,
            totalRedacted = perModuleStats.Values.Sum(),
        });

        var summary = Domain.Entities.AuditEntry.Create(
            tenantId: parameters.TenantId,
            module: "audit",
            operation: "gdpr_erasure_scan",
            operationType: nameof(OperationType.Action),
            userId: null,
            userEmail: "system:audit-payload-scan",
            ipAddress: null,
            userAgent: null,
            correlationId: null,
            isSuccess: true,
            errorKey: null,
            entityType: "Contact",
            entityId: parameters.ContactId.ToString("D"),
            beforeState: null,
            afterState: null,
            changes: null,
            metadata: metadata,
            timestamp: DateTimeOffset.UtcNow);

        dbContext.AuditEntries.Add(summary);
        await dbContext.SaveChangesAsync(ct);
    }
}
