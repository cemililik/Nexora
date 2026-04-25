using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Constants;
using Nexora.SharedKernel.Domain.Events;
using NSubstitute;

namespace Nexora.Modules.Notifications.Tests.Infrastructure.IntegrationEvents;

public sealed class ContactGdprDeletedIntegrationEventHandlerTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IInboxGuard _inboxGuard;
    private readonly HashSet<Guid> _processedEventIds = [];
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public ContactGdprDeletedIntegrationEventHandlerTests()
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
    public async Task HandleAsync_ErasedContact_ShouldScrubRecipientAddressesAndBodies()
    {
        // Arrange
        var targetContactId = Guid.NewGuid();
        var unrelatedContactId = Guid.NewGuid();
        var (targetNotificationId, _) = await SeedNotification(targetContactId, "target@example.com", "Hi John, your receipt is $100.");
        var (unrelatedNotificationId, _) = await SeedNotification(unrelatedContactId, "other@example.com", "Hello Jane.");

        var handler = CreateHandler();
        var @event = CreateEvent(targetContactId);

        // Act
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert — target scrubbed
        var targetNotification = await _dbContext.Notifications
            .Include(n => n.Recipients)
            .FirstAsync(n => n.Id == targetNotificationId);
        targetNotification.BodyRendered.Should().BeNull(
            "T-017: BodyRendered is nullable and the scrub path writes null (not a placeholder) so auditors can distinguish an erased row from real content.");
        targetNotification.Recipients.Should().OnlyContain(r => r.RecipientAddress == PiiRedactedPlaceholder.Value);

        // Assert — unrelated untouched
        var unrelatedNotification = await _dbContext.Notifications
            .Include(n => n.Recipients)
            .FirstAsync(n => n.Id == unrelatedNotificationId);
        unrelatedNotification.BodyRendered.Should().Be("Hello Jane.");
        unrelatedNotification.Recipients.Should().OnlyContain(r => r.RecipientAddress == "other@example.com");
    }

    [Fact]
    public async Task HandleAsync_AuditMetadataPreserved_AfterScrubbing()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var (notificationId, _) = await SeedNotification(contactId, "user@example.com", "PII body");
        var handler = CreateHandler();

        // Act
        await handler.HandleAsync(CreateEvent(contactId), CancellationToken.None);

        // Assert — status, timestamps, channel, triggered-by preserved; Subject scrubbed
        var notification = await _dbContext.Notifications
            .Include(n => n.Recipients)
            .FirstAsync(n => n.Id == notificationId);
        notification.Status.Should().Be(NotificationStatus.Queued);
        notification.Channel.Should().Be(NotificationChannel.Email);
        notification.Subject.Should().Be(PiiRedactedPlaceholder.Value,
            "Subject may contain PII interpolations and must be scrubbed alongside BodyRendered");
        notification.TriggeredBy.Should().Be("test");
        notification.Recipients.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsync_FailureReason_ShouldBeScrubbedAlongsideRecipientAddress()
    {
        // Arrange — failure_reason can contain PII (e.g., "Bounced: john@example.com unreachable").
        var contactId = Guid.NewGuid();
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Subject", "Body", "test");
        var recipient = notification.AddRecipient(contactId, "user@example.com");
        recipient.MarkSent("provider-msg-1");
        recipient.MarkFailed("Bounced: user@example.com unreachable");
        notification.ClearDomainEvents();
        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();

        // Act
        await handler.HandleAsync(CreateEvent(contactId), CancellationToken.None);

        // Assert
        var scrubbed = await _dbContext.Notifications
            .Include(n => n.Recipients)
            .FirstAsync();
        scrubbed.Recipients.Should().OnlyContain(r =>
            r.RecipientAddress == PiiRedactedPlaceholder.Value && r.FailureReason == null);
    }

    [Fact]
    public async Task HandleAsync_RunTwice_SecondRunIsIdempotentNoOp()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        await SeedNotification(contactId, "user@example.com", "PII body");
        var handler = CreateHandler();
        var @event = CreateEvent(contactId);

        // Act — first run scrubs
        await handler.HandleAsync(@event, CancellationToken.None);
        var afterFirstRun = await _dbContext.Notifications.AsNoTracking().FirstAsync();
        afterFirstRun.BodyRendered.Should().BeNull("T-017: scrubbed body is null.");

        // Act — second run (same EventId) must short-circuit via inbox guard
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert — values unchanged, and no extra MarkAsProcessed invocation for this EventId
        var afterSecondRun = await _dbContext.Notifications.AsNoTracking().FirstAsync();
        afterSecondRun.BodyRendered.Should().BeNull("T-017: scrubbed body stays null across idempotent re-runs.");
        _inboxGuard.Received(1).MarkAsProcessed(@event.EventId, nameof(ContactGdprDeletedIntegrationEvent));
    }

    [Fact]
    public async Task HandleAsync_NoMatchingRecipients_ShouldMarkInboxAndReturn()
    {
        // Arrange
        await SeedNotification(Guid.NewGuid(), "unrelated@example.com", "Body");
        var handler = CreateHandler();
        var @event = CreateEvent(Guid.NewGuid());

        // Act
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert — notification untouched, inbox marked
        var notification = await _dbContext.Notifications.FirstAsync();
        notification.BodyRendered.Should().Be("Body");
        _inboxGuard.Received(1).MarkAsProcessed(@event.EventId, nameof(ContactGdprDeletedIntegrationEvent));
    }

    [Fact]
    public async Task HandleAsync_InvalidTenantId_ShouldMarkProcessedToAvoidPoison()
    {
        // Arrange — malformed TenantId must not spin in redelivery loops. The handler
        // logs a warning and marks the event as processed so the broker stops retrying.
        var contactId = Guid.NewGuid();
        await SeedNotification(contactId, "user@example.com", "Body");
        var handler = CreateHandler();
        var @event = new ContactGdprDeletedIntegrationEvent
        {
            TenantId = "not-a-guid",
            ContactId = contactId,
            Reason = "dsr",
            Mode = "anonymized",
            DeletedAtUtc = DateTime.UtcNow,
            ErasedByUserId = Guid.NewGuid()
        };

        // Act
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert — no scrubbing, but inbox IS marked so the broker won't redeliver forever.
        var notification = await _dbContext.Notifications.FirstAsync();
        notification.BodyRendered.Should().Be("Body");
        _inboxGuard.Received(1).MarkAsProcessed(@event.EventId, nameof(ContactGdprDeletedIntegrationEvent));
    }

    private ContactGdprDeletedIntegrationEventHandler CreateHandler() =>
        new(_dbContext, _inboxGuard,
            new Nexora.Infrastructure.Gdpr.NoOpGdprRenamedTableScanner<Nexora.Modules.Notifications.Infrastructure.NotificationsDbContext>(),
            NullLogger<ContactGdprDeletedIntegrationEventHandler>.Instance);

    private ContactGdprDeletedIntegrationEvent CreateEvent(Guid contactId) => new()
    {
        TenantId = _tenantId.ToString(),
        ContactId = contactId,
        Reason = "data subject request",
        Mode = "anonymized",
        DeletedAtUtc = DateTime.UtcNow,
        ErasedByUserId = Guid.NewGuid()
    };

    private async Task<(NotificationId NotificationId, NotificationRecipientId RecipientId)> SeedNotification(
        Guid contactId, string recipientAddress, string body)
    {
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Subject", body, "test");
        var recipient = notification.AddRecipient(contactId, recipientAddress);
        notification.ClearDomainEvents();
        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.SaveChangesAsync();
        return (notification.Id, recipient.Id);
    }

    public void Dispose() => _dbContext.Dispose();
}
