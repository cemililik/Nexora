using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Notifications.Tests.Domain;

public sealed class NotificationScheduleTests
{
    private readonly NotificationId _notificationId = NotificationId.New();

    [Fact]
    public void Create_WithFutureDate_ReturnsSchedule()
    {
        // Arrange
        var scheduledAt = DateTime.UtcNow.AddHours(1);

        // Act
        var schedule = NotificationSchedule.Create(_notificationId, scheduledAt);

        // Assert
        schedule.Id.Value.Should().NotBeEmpty();
        schedule.NotificationId.Should().Be(_notificationId);
        schedule.ScheduledAt.Should().Be(scheduledAt);
        schedule.Status.Should().Be(ScheduleStatus.Pending);
    }

    [Fact]
    public void Create_WithPastDate_ThrowsDomainException()
    {
        // Arrange
        var pastDate = DateTime.UtcNow.AddHours(-1);

        // Act
        var act = () => NotificationSchedule.Create(_notificationId, pastDate);

        // Assert
        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_notifications_error_schedule_must_be_future");
    }

    [Fact]
    public void Dispatch_FromPending_TransitionsToDispatched()
    {
        // Arrange
        var schedule = NotificationSchedule.Create(_notificationId, DateTime.UtcNow.AddHours(1));

        // Act
        schedule.Dispatch();

        // Assert
        schedule.Status.Should().Be(ScheduleStatus.Dispatched);
    }

    [Fact]
    public void Cancel_FromPending_TransitionsToCancelled()
    {
        // Arrange
        var schedule = NotificationSchedule.Create(_notificationId, DateTime.UtcNow.AddHours(1));

        // Act
        schedule.Cancel();

        // Assert
        schedule.Status.Should().Be(ScheduleStatus.Cancelled);
    }

    [Fact]
    public void Dispatch_FromNonPending_ThrowsDomainException()
    {
        // Arrange
        var schedule = NotificationSchedule.Create(_notificationId, DateTime.UtcNow.AddHours(1));
        schedule.Cancel();

        // Act
        var act = () => schedule.Dispatch();

        // Assert
        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_notifications_error_schedule_not_pending");
    }

    [Fact]
    public void Cancel_FromNonPending_ThrowsDomainException()
    {
        // Arrange
        var schedule = NotificationSchedule.Create(_notificationId, DateTime.UtcNow.AddHours(1));
        schedule.Dispatch();

        // Act
        var act = () => schedule.Cancel();

        // Assert
        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_notifications_error_schedule_not_pending");
    }
}
