using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Infrastructure.Persistence.Inbox;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Infrastructure;
using Nexora.Modules.Audit.Infrastructure.IntegrationEvents;
using Nexora.SharedKernel.Domain.Events;
using OperationType = Nexora.SharedKernel.Abstractions.Audit.OperationType;

namespace Nexora.Modules.Audit.Tests.Infrastructure;

public sealed class ContactGdprDeletedIntegrationEventHandlerTests : IDisposable
{
    private readonly AuditDbContext _dbContext;
    private readonly string _tenantId = Guid.NewGuid().ToString();

    public ContactGdprDeletedIntegrationEventHandlerTests()
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(_tenantId);

        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AuditDbContext(options, accessor);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task HandleAsync_RedactsMatchingEntries_AppendsComplianceRecord_AndIsIdempotent()
    {
        // Arrange — seed 3 entries: 2 for contact X, 1 unrelated (contact Y)
        var contactX = Guid.NewGuid();
        var contactY = Guid.NewGuid();
        var erasedBy = Guid.NewGuid();

        var originalBeforeX = "{\"email\":\"victim@example.com\",\"name\":\"Alice\"}";
        var originalAfterX = "{\"email\":\"victim+new@example.com\",\"name\":\"Alice A.\"}";
        var originalChangesX = "{\"email\":[\"victim@example.com\",\"victim+new@example.com\"]}";
        var unrelatedBefore = "{\"email\":\"bystander@example.com\"}";

        var entryX1 = AuditEntry.Create(
            tenantId: _tenantId, module: "contacts", operation: "UpdateContact", operationType: "Action",
            userId: Guid.NewGuid(), userEmail: "operator@test.com", ipAddress: "10.0.0.1", userAgent: "ua",
            correlationId: "corr-X1", isSuccess: true, errorKey: null,
            entityType: "Contact", entityId: contactX.ToString(),
            beforeState: originalBeforeX, afterState: originalAfterX, changes: originalChangesX,
            metadata: "{\"source\":\"api\"}", timestamp: DateTimeOffset.UtcNow.AddDays(-3));

        var entryX2 = AuditEntry.Create(
            tenantId: _tenantId, module: "contacts", operation: "DeleteContact", operationType: "Action",
            userId: Guid.NewGuid(), userEmail: "operator2@test.com", ipAddress: "10.0.0.2", userAgent: "ua2",
            correlationId: "corr-X2", isSuccess: true, errorKey: null,
            entityType: "Contact", entityId: contactX.ToString(),
            beforeState: originalBeforeX, afterState: null, changes: null,
            metadata: null, timestamp: DateTimeOffset.UtcNow.AddDays(-1));

        var entryY = AuditEntry.Create(
            tenantId: _tenantId, module: "contacts", operation: "UpdateContact", operationType: "Action",
            userId: Guid.NewGuid(), userEmail: "operator3@test.com", ipAddress: "10.0.0.3", userAgent: "ua3",
            correlationId: "corr-Y", isSuccess: true, errorKey: null,
            entityType: "Contact", entityId: contactY.ToString(),
            beforeState: unrelatedBefore, afterState: null, changes: null,
            metadata: null, timestamp: DateTimeOffset.UtcNow.AddDays(-2));

        _dbContext.AuditEntries.AddRange(entryX1, entryX2, entryY);
        await _dbContext.SaveChangesAsync();

        var deletedAt = new DateTime(2026, 4, 23, 0, 0, 0, DateTimeKind.Utc);
        var @event = new ContactGdprDeletedIntegrationEvent
        {
            TenantId = _tenantId,
            ContactId = contactX,
            Reason = "data subject request",
            Mode = "hard_deleted",
            DeletedAtUtc = deletedAt,
            ErasedByUserId = erasedBy
        };

        var inboxGuard = new InboxGuard<AuditDbContext>(_dbContext);
        var backgroundJobClient = NSubstitute.Substitute.For<Hangfire.IBackgroundJobClient>();
        var handler = new ContactGdprDeletedIntegrationEventHandler(
            _dbContext, inboxGuard, backgroundJobClient,
            NullLogger<ContactGdprDeletedIntegrationEventHandler>.Instance);

        // Act — first run
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert — X entries redacted, Y untouched, compliance record appended
        _dbContext.ChangeTracker.Clear();

        var allEntries = await _dbContext.AuditEntries.ToListAsync();
        allEntries.Should().HaveCount(4, "3 seeded + 1 appended compliance record");

        var redactedX = allEntries.Where(e =>
            e.EntityType == "Contact"
            && e.EntityId == contactX.ToString()
            && e.Operation != "gdpr_erasure").ToList();
        redactedX.Should().HaveCount(2);

        foreach (var entry in redactedX)
        {
            // BeforeState is non-null on both seeded entries, so it is always the redaction marker.
            // AfterState / Changes were null on entryX2 and MUST remain null — the redaction path
            // preserves nulls so downstream readers don't mistake "no payload" for "payload redacted".
            entry.BeforeState.Should().NotBeNull();

            using var beforeDoc = JsonDocument.Parse(entry.BeforeState!);
            beforeDoc.RootElement.GetProperty("_redacted").GetBoolean().Should().BeTrue();
            beforeDoc.RootElement.GetProperty("_reason").GetString().Should().Be("gdpr_erasure");
            // GUID formatted with "D" (dashed canonical form) — matches audit pipeline convention.
            beforeDoc.RootElement.GetProperty("_erasedByUserId").GetString()
                .Should().Be(erasedBy.ToString("D"));
            // ISO-8601 round-trip "O" format — millisecond precision + timezone marker.
            beforeDoc.RootElement.GetProperty("_erasedAtUtc").GetString()
                .Should().Be(DateTime.SpecifyKind(deletedAt, DateTimeKind.Utc).ToString("O"));

            // Operational trace preserved (the whole point of redact-not-delete).
            entry.Operation.Should().NotBeNullOrEmpty();
            entry.EntityType.Should().Be("Contact");
            entry.EntityId.Should().Be(contactX.ToString());
            entry.IsSuccess.Should().BeTrue();
            entry.CorrelationId.Should().NotBeNullOrEmpty();
        }

        // Unrelated entry (contact Y) must be untouched.
        var persistedY = allEntries.Single(e => e.EntityId == contactY.ToString());
        persistedY.BeforeState.Should().Be(unrelatedBefore);
        persistedY.AfterState.Should().BeNull();
        persistedY.Changes.Should().BeNull();

        // Compliance record appended (append, not redaction).
        var erasureRecord = allEntries.Single(e => e.Operation == "gdpr_erasure");
        erasureRecord.EntityType.Should().Be("Contact");
        erasureRecord.EntityId.Should().Be(contactX.ToString());
        erasureRecord.UserId.Should().Be(erasedBy);
        erasureRecord.Module.Should().Be("contacts");
        erasureRecord.OperationType.Should().Be(nameof(OperationType.Action));
        erasureRecord.IsSuccess.Should().BeTrue();
        using (var payloadDoc = JsonDocument.Parse(erasureRecord.AfterState!))
        {
            payloadDoc.RootElement.GetProperty("reason").GetString().Should().Be("data subject request");
            payloadDoc.RootElement.GetProperty("mode").GetString().Should().Be("hard_deleted");
        }

        // Inbox must contain the event id.
        var inboxCount = await _dbContext.InboxMessages.CountAsync(m => m.EventId == @event.EventId);
        inboxCount.Should().Be(1);

        // Act — second run (idempotency)
        await handler.HandleAsync(@event, CancellationToken.None);

        _dbContext.ChangeTracker.Clear();
        var afterSecondRun = await _dbContext.AuditEntries.ToListAsync();
        afterSecondRun.Should().HaveCount(4, "idempotent — no new compliance record appended on replay");

        var gdprCount = afterSecondRun.Count(e => e.Operation == "gdpr_erasure");
        gdprCount.Should().Be(1);
    }
}
