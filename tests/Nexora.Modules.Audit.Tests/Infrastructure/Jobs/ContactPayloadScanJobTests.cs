using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Audit.Infrastructure;
using Nexora.Modules.Audit.Infrastructure.Jobs;
using Nexora.SharedKernel.Abstractions.Audit;
using AuditEntry = Nexora.Modules.Audit.Domain.Entities.AuditEntry;

namespace Nexora.Modules.Audit.Tests.Infrastructure.Jobs;

/// <summary>
/// T-010 (ADR-0026) tests for the cross-module audit-payload scan job.
/// Drives the job through stub locators so the test exercises the
/// scaffolding contract independently of any specific module — tests
/// here verify the wiring; consumer modules (CRM Phase 2, etc.) test
/// their own redaction rules in their own test suites.
/// </summary>
public sealed class ContactPayloadScanJobTests : IDisposable
{
    private readonly AuditDbContext _dbContext;
    private readonly TenantContextAccessor _accessor;
    private readonly string _tenantId = Guid.NewGuid().ToString();

    public ContactPayloadScanJobTests()
    {
        _accessor = new TenantContextAccessor();
        _accessor.SetTenant(_tenantId);

        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AuditDbContext(options, _accessor);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task ExecuteAsync_NoLocators_AppendsSummaryOnly()
    {
        var contactId = Guid.NewGuid();
        var job = CreateJob([]);

        await job.RunAsync(
            new ContactPayloadScanJobParams { TenantId = _tenantId, ContactId = contactId },
            CancellationToken.None);

        var summary = await GetScanSummaryAsync(contactId);
        summary.Should().NotBeNull();
        var doc = JsonDocument.Parse(summary!.Metadata!);
        doc.RootElement.GetProperty("totalRedacted").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_LocatorMatches_RedactsThatModule_AndAppendsSummary()
    {
        var contactId = Guid.NewGuid();

        // Two CRM entries — one references contactId, one does not.
        var matching = AuditEntry.Create(
            tenantId: _tenantId, module: "crm", operation: "UpdateLead", operationType: "Action",
            userId: null, userEmail: null, ipAddress: null, userAgent: null, correlationId: null,
            isSuccess: true, errorKey: null,
            entityType: "Lead", entityId: Guid.NewGuid().ToString(),
            beforeState: $$"""{"assignedTo":"{{contactId}}","title":"Lead 1"}""",
            afterState: null, changes: null, metadata: null,
            timestamp: DateTimeOffset.UtcNow);

        var unrelated = AuditEntry.Create(
            tenantId: _tenantId, module: "crm", operation: "UpdateLead", operationType: "Action",
            userId: null, userEmail: null, ipAddress: null, userAgent: null, correlationId: null,
            isSuccess: true, errorKey: null,
            entityType: "Lead", entityId: Guid.NewGuid().ToString(),
            beforeState: $$"""{"assignedTo":"{{Guid.NewGuid()}}","title":"Lead 2"}""",
            afterState: null, changes: null, metadata: null,
            timestamp: DateTimeOffset.UtcNow);

        await _dbContext.AuditEntries.AddRangeAsync(matching, unrelated);
        await _dbContext.SaveChangesAsync();

        var locator = new StubAssignedToLocator("crm");
        var job = CreateJob([locator]);

        await job.RunAsync(
            new ContactPayloadScanJobParams { TenantId = _tenantId, ContactId = contactId },
            CancellationToken.None);

        // Re-load — InMemory tracker can hold stale references.
        _dbContext.ChangeTracker.Clear();
        var reloadedMatch = await _dbContext.AuditEntries.FindAsync(matching.Id);
        var reloadedUnrelated = await _dbContext.AuditEntries.FindAsync(unrelated.Id);

        reloadedMatch!.BeforeState.Should().Contain("[REDACTED-CONTACT]");
        reloadedUnrelated!.BeforeState.Should().NotContain("[REDACTED-CONTACT]");

        var summary = await GetScanSummaryAsync(contactId);
        var doc = JsonDocument.Parse(summary!.Metadata!);
        doc.RootElement.GetProperty("totalRedacted").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("perModuleRedacted").GetProperty("crm").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_OnlyTouchesEntriesFromMatchingModule()
    {
        // CRM locator is registered. A "documents" entry that happens to
        // mention the contact MUST NOT be touched — locator's ModuleName
        // gates the scan to its own module's rows.
        var contactId = Guid.NewGuid();
        var docsEntry = AuditEntry.Create(
            tenantId: _tenantId, module: "documents", operation: "Update", operationType: "Action",
            userId: null, userEmail: null, ipAddress: null, userAgent: null, correlationId: null,
            isSuccess: true, errorKey: null,
            entityType: "Document", entityId: Guid.NewGuid().ToString(),
            beforeState: $$"""{"assignedTo":"{{contactId}}"}""",
            afterState: null, changes: null, metadata: null,
            timestamp: DateTimeOffset.UtcNow);
        await _dbContext.AuditEntries.AddAsync(docsEntry);
        await _dbContext.SaveChangesAsync();

        var crmLocator = new StubAssignedToLocator("crm"); // mismatched module
        var job = CreateJob([crmLocator]);

        await job.RunAsync(
            new ContactPayloadScanJobParams { TenantId = _tenantId, ContactId = contactId },
            CancellationToken.None);

        _dbContext.ChangeTracker.Clear();
        var reloaded = await _dbContext.AuditEntries.FindAsync(docsEntry.Id);
        reloaded!.BeforeState.Should().NotContain("[REDACTED-CONTACT]");
    }

    [Fact]
    public async Task ExecuteAsync_RunTwice_IsIdempotent_SecondPassIsNoOp()
    {
        var contactId = Guid.NewGuid();
        var entry = AuditEntry.Create(
            tenantId: _tenantId, module: "crm", operation: "UpdateLead", operationType: "Action",
            userId: null, userEmail: null, ipAddress: null, userAgent: null, correlationId: null,
            isSuccess: true, errorKey: null,
            entityType: "Lead", entityId: Guid.NewGuid().ToString(),
            beforeState: $$"""{"assignedTo":"{{contactId}}"}""",
            afterState: null, changes: null, metadata: null,
            timestamp: DateTimeOffset.UtcNow);
        await _dbContext.AuditEntries.AddAsync(entry);
        await _dbContext.SaveChangesAsync();

        var job = CreateJob([new StubAssignedToLocator("crm")]);

        await job.RunAsync(
            new ContactPayloadScanJobParams { TenantId = _tenantId, ContactId = contactId },
            CancellationToken.None);
        await job.RunAsync(
            new ContactPayloadScanJobParams { TenantId = _tenantId, ContactId = contactId },
            CancellationToken.None);

        // Two runs → two summary rows (both visible to ops), but the entry
        // is redacted exactly once (the locator returns null on already-
        // redacted payload).
        var summaries = await _dbContext.AuditEntries
            .Where(e => e.Operation == "gdpr_erasure_scan" && e.EntityId == contactId.ToString("D"))
            .ToListAsync();
        summaries.Should().HaveCount(2);

        var firstTotal = JsonDocument.Parse(summaries[0].Metadata!)
            .RootElement.GetProperty("totalRedacted").GetInt32();
        var secondTotal = JsonDocument.Parse(summaries[1].Metadata!)
            .RootElement.GetProperty("totalRedacted").GetInt32();

        (firstTotal + secondTotal).Should().Be(1, "the second pass must be a no-op");
    }

    // --- helpers ---------------------------------------------------------

    private ContactPayloadScanJob CreateJob(IEnumerable<IContactReferenceLocator> locators) =>
        new(_accessor, _dbContext, locators, NullLogger<ContactPayloadScanJob>.Instance);

    private async Task<AuditEntry?> GetScanSummaryAsync(Guid contactId)
    {
        return await _dbContext.AuditEntries
            .Where(e => e.Operation == "gdpr_erasure_scan" && e.EntityId == contactId.ToString("D"))
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Stub locator: redacts a top-level <c>"assignedTo"</c> property whose
    /// value matches the erased contact ID. Keeps the rest of the payload.
    /// Idempotent — a second pass over already-redacted JSON returns null.
    /// </summary>
    private sealed class StubAssignedToLocator(string moduleName) : IContactReferenceLocator
    {
        public string ModuleName { get; } = moduleName;

        public string? Redact(string? jsonPayload, Guid contactId)
        {
            if (string.IsNullOrWhiteSpace(jsonPayload)) return null;
            using var doc = JsonDocument.Parse(jsonPayload);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!doc.RootElement.TryGetProperty("assignedTo", out var assigned)) return null;
            var value = assigned.GetString();
            if (value != contactId.ToString() && value != contactId.ToString("D")) return null;

            // Build a new object replacing assignedTo with the redaction marker.
            var dict = new Dictionary<string, object?>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                dict[prop.Name] = prop.Name == "assignedTo"
                    ? "[REDACTED-CONTACT]"
                    : JsonSerializer.Deserialize<object?>(prop.Value.GetRawText());
            }
            return JsonSerializer.Serialize(dict);
        }
    }
}
