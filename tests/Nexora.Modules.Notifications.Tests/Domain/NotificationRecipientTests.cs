using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Notifications.Tests.Domain;

public sealed class NotificationRecipientTests
{
    private readonly NotificationId _notificationId = NotificationId.New();
    private readonly Guid _contactId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_ReturnsRecipient()
    {
        // Arrange & Act
        var recipient = NotificationRecipient.Create(_notificationId, _contactId, "user@example.com");

        // Assert
        recipient.Id.Value.Should().NotBeEmpty();
        recipient.NotificationId.Should().Be(_notificationId);
        recipient.ContactId.Should().Be(_contactId);
        recipient.RecipientAddress.Should().Be("user@example.com");
        recipient.Status.Should().Be(RecipientStatus.Pending);
        recipient.SentAt.Should().BeNull();
    }

    [Fact]
    public void MarkSent_FromPending_SetsStatusAndTimestamp()
    {
        // Arrange
        var recipient = NotificationRecipient.Create(_notificationId, _contactId, "user@example.com");

        // Act
        recipient.MarkSent("msg_123");

        // Assert
        recipient.Status.Should().Be(RecipientStatus.Sent);
        recipient.ProviderMessageId.Should().Be("msg_123");
        recipient.SentAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkDelivered_FromSent_SetsDeliveredStatus()
    {
        // Arrange
        var recipient = NotificationRecipient.Create(_notificationId, _contactId, "user@example.com");
        recipient.MarkSent("msg_123");

        // Act
        recipient.MarkDelivered();

        // Assert
        recipient.Status.Should().Be(RecipientStatus.Delivered);
        recipient.DeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkOpened_FromDelivered_SetsOpenedStatus()
    {
        // Arrange
        var recipient = NotificationRecipient.Create(_notificationId, _contactId, "user@example.com");
        recipient.MarkSent("msg_123");
        recipient.MarkDelivered();

        // Act
        recipient.MarkOpened();

        // Assert
        recipient.Status.Should().Be(RecipientStatus.Opened);
        recipient.OpenedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkClicked_FromOpened_SetsClickedStatus()
    {
        // Arrange
        var recipient = NotificationRecipient.Create(_notificationId, _contactId, "user@example.com");
        recipient.MarkSent("msg_123");
        recipient.MarkDelivered();
        recipient.MarkOpened();

        // Act
        recipient.MarkClicked();

        // Assert
        recipient.Status.Should().Be(RecipientStatus.Clicked);
    }

    [Fact]
    public void MarkBounced_FromSent_SetsBouncedWithReason()
    {
        // Arrange
        var recipient = NotificationRecipient.Create(_notificationId, _contactId, "user@example.com");
        recipient.MarkSent("msg_123");

        // Act
        recipient.MarkBounced("Mailbox full");

        // Assert
        recipient.Status.Should().Be(RecipientStatus.Bounced);
        recipient.FailureReason.Should().Be("Mailbox full");
    }

    [Fact]
    public void MarkFailed_FromPending_SetsFailedWithReason()
    {
        // Arrange
        var recipient = NotificationRecipient.Create(_notificationId, _contactId, "user@example.com");

        // Act
        recipient.MarkFailed("Provider unavailable");

        // Assert
        recipient.Status.Should().Be(RecipientStatus.Failed);
        recipient.FailureReason.Should().Be("Provider unavailable");
    }

    [Fact]
    public void MarkSent_FromNonPending_ThrowsDomainException()
    {
        // Arrange
        var recipient = NotificationRecipient.Create(_notificationId, _contactId, "user@example.com");
        recipient.MarkSent("msg_123");

        // Act
        var act = () => recipient.MarkSent("msg_456");

        // Assert
        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_notifications_error_recipient_invalid_transition");
    }
}
