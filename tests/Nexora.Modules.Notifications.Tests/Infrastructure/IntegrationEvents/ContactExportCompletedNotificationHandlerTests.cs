using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Nexora.Modules.Notifications.Tests.Infrastructure.IntegrationEvents;

/// <summary>
/// T-028 unit tests — the handler consumes <c>ContactExportCompletedIntegrationEvent</c>
/// emitted by <c>ContactExportJob</c>, sends the in-app "export ready" notification
/// via <see cref="INotificationService"/>, and uses the inbox guard (EventId) to
/// deduplicate redeliveries. Exactly-once end-to-end delivery: outbox atomicity
/// ensures one emission per export job; inbox guard collapses any redelivery of
/// that emission. Replaces the job-side <c>startedFromQueued</c> gate that had
/// the "missed notification on mid-run crash" trade-off.
/// </summary>
public sealed class ContactExportCompletedNotificationHandlerTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IInboxGuard _inboxGuard;
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
    private readonly HashSet<Guid> _processedEventIds = [];
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public ContactExportCompletedNotificationHandlerTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        _inboxGuard = Substitute.For<IInboxGuard>();
        _inboxGuard.IsAlreadyProcessedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => _processedEventIds.Contains(ci.ArgAt<Guid>(0)));
        _inboxGuard.When(g => g.MarkAsProcessed(Arg.Any<Guid>(), Arg.Any<string>()))
            .Do(ci => _processedEventIds.Add(ci.ArgAt<Guid>(0)));

        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task HandleAsync_FirstDelivery_SendsNotificationAndMarksProcessed()
    {
        var userId = Guid.NewGuid();
        var @event = CreateEvent(jobId: Guid.NewGuid(), triggeredByUserId: userId, totalRows: 42, format: "csv");
        var handler = CreateHandler();

        await handler.HandleAsync(@event, CancellationToken.None);

        await _notificationService.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(r =>
                r.TemplateCode == "lockey_contacts_notification_export_ready" &&
                r.Channel == "in_app" &&
                r.ContactId == userId &&
                r.Variables!["jobId"] == @event.JobId.ToString() &&
                r.Variables!["format"] == "csv" &&
                r.Variables!["totalRows"] == "42"),
            Arg.Any<CancellationToken>());

        _processedEventIds.Should().Contain(@event.EventId,
            "the inbox guard must record the EventId so a redelivery short-circuits.");
    }

    [Fact]
    public async Task HandleAsync_DuplicateRedelivery_SkipsSendAndReturnsEarly()
    {
        var userId = Guid.NewGuid();
        var @event = CreateEvent(jobId: Guid.NewGuid(), triggeredByUserId: userId);
        var handler = CreateHandler();

        // First delivery: send + mark.
        await handler.HandleAsync(@event, CancellationToken.None);
        _notificationService.ClearReceivedCalls();

        // Second delivery of the same EventId: inbox guard must short-circuit.
        await handler.HandleAsync(@event, CancellationToken.None);

        await _notificationService.DidNotReceive().SendAsync(
            Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NullTriggeredByUserId_SkipsSendButMarksInbox()
    {
        // An anonymous or scheduled export has no user to notify — the user can
        // still observe completion via the status-polling endpoint. Mark the
        // inbox so redeliveries don't reprocess.
        var @event = CreateEvent(jobId: Guid.NewGuid(), triggeredByUserId: null);
        var handler = CreateHandler();

        await handler.HandleAsync(@event, CancellationToken.None);

        await _notificationService.DidNotReceive().SendAsync(
            Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>());
        _processedEventIds.Should().Contain(@event.EventId);
    }

    [Fact]
    public async Task HandleAsync_InvalidTenantId_SkipsSendButMarksInboxAgainstRedeliveryLoop()
    {
        var @event = new ContactExportCompletedIntegrationEvent
        {
            TenantId = "not-a-guid",
            JobId = Guid.NewGuid(),
            TotalRows = 5,
            Format = "csv",
            StorageKey = "k",
            TriggeredByUserId = Guid.NewGuid(),
            CompletedAtUtc = DateTime.UtcNow
        };
        var handler = CreateHandler();

        await handler.HandleAsync(@event, CancellationToken.None);

        await _notificationService.DidNotReceive().SendAsync(
            Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>());
        _processedEventIds.Should().Contain(@event.EventId,
            "invalid tenant id is a poison message — mark processed so it doesn't retry forever.");
    }

    [Fact]
    public async Task HandleAsync_SendAsyncThrows_DoesNotMarkInboxSoRedeliveryRetries()
    {
        // The notification send is best-effort: if it fails, the inbox stays
        // unmarked so the next redelivery of the SAME EventId will retry.
        // This is distinct from the invalid-payload case above, which IS
        // marked to prevent an infinite retry loop.
        var userId = Guid.NewGuid();
        var @event = CreateEvent(jobId: Guid.NewGuid(), triggeredByUserId: userId);
        _notificationService
            .SendAsync(Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("template not found"));

        var handler = CreateHandler();
        await handler.HandleAsync(@event, CancellationToken.None);

        _processedEventIds.Should().NotContain(@event.EventId,
            "transient send failures must leave the inbox unmarked so a future redelivery gets another chance.");
    }

    // --- Helpers -------------------------------------------------------------

    private ContactExportCompletedNotificationHandler CreateHandler() =>
        new(_dbContext, _notificationService, _inboxGuard,
            NullLogger<ContactExportCompletedNotificationHandler>.Instance);

    private ContactExportCompletedIntegrationEvent CreateEvent(
        Guid jobId, Guid? triggeredByUserId, int totalRows = 10, string format = "csv") =>
        new()
        {
            TenantId = _tenantId.ToString(),
            JobId = jobId,
            TotalRows = totalRows,
            Format = format,
            StorageKey = $"{_orgId}/contacts/exports/{jobId}.{format}",
            TriggeredByUserId = triggeredByUserId,
            CompletedAtUtc = DateTime.UtcNow
        };

    public void Dispose() => _dbContext.Dispose();
}
