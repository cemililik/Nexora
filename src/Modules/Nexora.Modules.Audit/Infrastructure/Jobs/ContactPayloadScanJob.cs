using System.ComponentModel;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
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

        foreach (var locator in locatorList)
        {
            ct.ThrowIfCancellationRequested();
            var redactedThisModule = 0;

            // Fetch only this locator's module rows. Tenant + module index
            // is on AuditEntry — see AuditEntryConfiguration. Materializing
            // here is acceptable because real production sites will partition
            // audit_entries by month and the scan runs against ~1 month of
            // data per locator (rough order; the 10M-row benchmark is
            // deferred to Milestone C per the task reclassification).
            var entries = await dbContext.AuditEntries
                .Where(e => e.TenantId == parameters.TenantId && e.Module == locator.ModuleName)
                .ToListAsync(ct);

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                var redactedBefore = locator.Redact(entry.BeforeState, parameters.ContactId);
                var redactedAfter = locator.Redact(entry.AfterState, parameters.ContactId);
                var redactedChanges = locator.Redact(entry.Changes, parameters.ContactId);

                if (redactedBefore is null && redactedAfter is null && redactedChanges is null)
                    continue; // nothing to redact in this entry

                entry.ApplyLocatorRedaction(redactedBefore, redactedAfter, redactedChanges);
                redactedThisModule++;
            }

            if (redactedThisModule > 0)
                perModuleStats[locator.ModuleName] = redactedThisModule;

            totalRedacted += redactedThisModule;
        }

        if (totalRedacted > 0)
            await dbContext.SaveChangesAsync(ct);

        await AppendSummaryAuditEntryAsync(parameters, perModuleStats, ct);

        logger.LogInformation(
            "Contact payload scan completed: tenant {TenantId} contact {ContactId}; {TotalRedacted} entries redacted across {ModuleCount} module(s).",
            parameters.TenantId, parameters.ContactId, totalRedacted, perModuleStats.Count);
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
