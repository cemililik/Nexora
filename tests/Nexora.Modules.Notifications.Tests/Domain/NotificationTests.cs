using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.Events;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Notifications.Tests.Domain;

public sealed class NotificationTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_ReturnsNotification()
    {
        // Arrange & Act
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Welcome!", "<p>Hello</p>", "identity.user.created");

        // Assert
        notification.Id.Value.Should().NotBeEmpty();
        notification.TenantId.Should().Be(_tenantId);
        notification.Channel.Should().Be(NotificationChannel.Email);
        notification.Status.Should().Be(NotificationStatus.Queued);
        notification.TotalRecipients.Should().Be(0);
        notification.QueuedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        notification.SentAt.Should().BeNull();
    }

    [Fact]
    public void Create_RaisesNotificationQueuedEvent()
    {
        // Arrange & Act
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Sms, "Alert", "Body", "manual");

        // Assert
        notification.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<NotificationQueuedEvent>()
            .Which.Channel.Should().Be(NotificationChannel.Sms);
    }

    [Fact]
    public void AddRecipient_IncrementsTotalRecipients()
    {
        // Arrange
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Subject", "Body", "test");
        var contactId = Guid.NewGuid();

        // Act
        var recipient = notification.AddRecipient(contactId, "user@example.com");

        // Assert
        notification.TotalRecipients.Should().Be(1);
        notification.Recipients.Should().HaveCount(1);
        recipient.ContactId.Should().Be(contactId);
        recipient.RecipientAddress.Should().Be("user@example.com");
    }

    [Fact]
    public void MarkSending_FromQueued_TransitionsToSending()
    {
        // Arrange
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Subject", "Body", "test");

        // Act
        notification.MarkSending();

        // Assert
        notification.Status.Should().Be(NotificationStatus.Sending);
    }

    [Fact]
    public void MarkSending_FromNonQueued_ThrowsDomainException()
    {
        // Arrange
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Subject", "Body", "test");
        notification.MarkSending();
        notification.MarkSent();

        // Act
        var act = () => notification.MarkSending();

        // Assert
        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_notifications_error_only_queued_can_send");
    }

    [Fact]
    public void MarkSent_FromSending_TransitionsToSent()
    {
        // Arrange
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Subject", "Body", "test");
        notification.MarkSending();
        notification.ClearDomainEvents();

        // Act
        notification.MarkSent();

        // Assert
        notification.Status.Should().Be(NotificationStatus.Sent);
        notification.SentAt.Should().NotBeNull();
        notification.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<NotificationSentEvent>();
    }

    [Fact]
    public void MarkFailed_FromSending_TransitionsToFailed()
    {
        // Arrange
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Subject", "Body", "test");
        notification.MarkSending();
        notification.ClearDomainEvents();

        // Act
        notification.MarkFailed();

        // Assert
        notification.Status.Should().Be(NotificationStatus.Failed);
        notification.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<NotificationFailedEvent>();
    }

    [Fact]
    public void MarkPartialFailure_FromSending_TransitionsToPartialFailure()
    {
        // Arrange
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Subject", "Body", "test");
        notification.MarkSending();

        // Act
        notification.MarkPartialFailure();

        // Assert
        notification.Status.Should().Be(NotificationStatus.PartialFailure);
        notification.SentAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateCounts_SetsCountValues()
    {
        // Arrange
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Subject", "Body", "test");

        // Act
        notification.UpdateCounts(delivered: 8, failed: 2, opened: 5, clicked: 3);

        // Assert
        notification.DeliveredCount.Should().Be(8);
        notification.FailedCount.Should().Be(2);
        notification.OpenedCount.Should().Be(5);
        notification.ClickedCount.Should().Be(3);
    }
}
