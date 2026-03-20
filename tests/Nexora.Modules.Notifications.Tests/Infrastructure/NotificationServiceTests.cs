using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Application.DTOs;
using Nexora.Modules.Notifications.Infrastructure.Services;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Tests.Infrastructure;

public sealed class NotificationServiceTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _service = new NotificationService(_sender, NullLogger<NotificationService>.Instance);
    }

    [Fact]
    public async Task SendAsync_ShouldSendViaMediator()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var dto = new NotificationDto(
            notificationId, "Email", "Subject", "Sending", "api", 1, 0, 0,
            DateTime.UtcNow, null);

        _sender.Send(Arg.Any<SendNotificationCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<NotificationDto>.Success(dto, LocalizedMessage.Of("lockey_notifications_notification_sent")));

        var request = new SendNotificationRequest(
            "welcome", "Email", Guid.NewGuid(),
            "user@test.com", new() { ["name"] = "Test" });

        // Act
        var result = await _service.SendAsync(request);

        // Assert
        result.Should().Be(notificationId);
        await _sender.Received(1).Send(Arg.Any<SendNotificationCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WhenFails_ShouldReturnEmptyGuid()
    {
        // Arrange
        _sender.Send(Arg.Any<SendNotificationCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<NotificationDto>.Failure("lockey_notifications_error_template_not_found"));

        var request = new SendNotificationRequest(
            "nonexistent", "Email", Guid.NewGuid(), "user@test.com", new());

        // Act
        var result = await _service.SendAsync(request);

        // Assert
        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task SendBulkAsync_ShouldSendViaMediator()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var dto = new BulkNotificationResultDto(notificationId, 1, 1, 0);

        _sender.Send(Arg.Any<SendBulkNotificationCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<BulkNotificationResultDto>.Success(dto, LocalizedMessage.Of("lockey_notifications_bulk_notification_queued")));

        var request = new SendBulkNotificationRequest(
            "welcome", "Email",
            [new BulkNotificationRecipient(Guid.NewGuid(), "user@test.com")],
            new());

        // Act
        var result = await _service.SendBulkAsync(request);

        // Assert
        result.Should().Be(notificationId);
        await _sender.Received(1).Send(Arg.Any<SendBulkNotificationCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScheduleAsync_ShouldSendViaMediator()
    {
        // Arrange
        var scheduleId = Guid.NewGuid();
        var dto = new NotificationScheduleDto(scheduleId, Guid.NewGuid(), DateTime.UtcNow.AddHours(1), "Pending", DateTimeOffset.UtcNow);

        _sender.Send(Arg.Any<ScheduleNotificationCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<NotificationScheduleDto>.Success(dto, LocalizedMessage.Of("lockey_notifications_notification_scheduled")));

        var request = new ScheduleNotificationRequest(
            "welcome", "Email", Guid.NewGuid(), "user@test.com",
            new(), DateTime.UtcNow.AddHours(1));

        // Act
        var result = await _service.ScheduleAsync(request);

        // Assert
        result.Should().Be(scheduleId);
        await _sender.Received(1).Send(Arg.Any<ScheduleNotificationCommand>(), Arg.Any<CancellationToken>());
    }
}
