using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Notifications.Tests.Domain;

public sealed class NotificationProviderTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_ReturnsProvider()
    {
        // Arrange & Act
        var provider = NotificationProvider.Create(
            _tenantId, NotificationChannel.Email, ProviderName.SendGrid,
            "{\"apiKey\":\"test\"}", dailyLimit: 1000, isDefault: true);

        // Assert
        provider.Id.Value.Should().NotBeEmpty();
        provider.TenantId.Should().Be(_tenantId);
        provider.Channel.Should().Be(NotificationChannel.Email);
        provider.ProviderName.Should().Be(ProviderName.SendGrid);
        provider.IsDefault.Should().BeTrue();
        provider.IsActive.Should().BeTrue();
        provider.DailyLimit.Should().Be(1000);
        provider.SentToday.Should().Be(0);
    }

    [Fact]
    public void Update_ChangesConfigAndLimits()
    {
        // Arrange
        var provider = NotificationProvider.Create(
            _tenantId, NotificationChannel.Email, ProviderName.SendGrid,
            "{}", dailyLimit: 500);

        // Act
        provider.Update("{\"apiKey\":\"new\"}", dailyLimit: 2000, isDefault: true);

        // Assert
        provider.Config.Should().Be("{\"apiKey\":\"new\"}");
        provider.DailyLimit.Should().Be(2000);
        provider.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ThrowsDomainException()
    {
        // Arrange
        var provider = NotificationProvider.Create(
            _tenantId, NotificationChannel.Email, ProviderName.SendGrid,
            "{}", dailyLimit: 1000);

        // Act
        var act = () => provider.Activate();

        // Assert
        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_notifications_error_provider_already_active");
    }

    [Fact]
    public void Deactivate_WhenActive_SetsInactive()
    {
        // Arrange
        var provider = NotificationProvider.Create(
            _tenantId, NotificationChannel.Email, ProviderName.SendGrid,
            "{}", dailyLimit: 1000);

        // Act
        provider.Deactivate();

        // Assert
        provider.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ThrowsDomainException()
    {
        // Arrange
        var provider = NotificationProvider.Create(
            _tenantId, NotificationChannel.Email, ProviderName.SendGrid,
            "{}", dailyLimit: 1000);
        provider.Deactivate();

        // Act
        var act = () => provider.Deactivate();

        // Assert
        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_notifications_error_provider_already_inactive");
    }

    [Fact]
    public void IncrementSentToday_WithinLimit_IncrementsSentCount()
    {
        // Arrange
        var provider = NotificationProvider.Create(
            _tenantId, NotificationChannel.Email, ProviderName.SendGrid,
            "{}", dailyLimit: 100);

        // Act
        provider.IncrementSentToday(5);

        // Assert
        provider.SentToday.Should().Be(5);
    }

    [Fact]
    public void IncrementSentToday_ExceedsLimit_ThrowsDomainException()
    {
        // Arrange
        var provider = NotificationProvider.Create(
            _tenantId, NotificationChannel.Email, ProviderName.SendGrid,
            "{}", dailyLimit: 10);
        provider.IncrementSentToday(10);

        // Act
        var act = () => provider.IncrementSentToday(1);

        // Assert
        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_notifications_error_provider_daily_limit_exceeded");
    }

    [Fact]
    public void ResetDailyCounter_ResetsToZero()
    {
        // Arrange
        var provider = NotificationProvider.Create(
            _tenantId, NotificationChannel.Email, ProviderName.SendGrid,
            "{}", dailyLimit: 100);
        provider.IncrementSentToday(50);

        // Act
        provider.ResetDailyCounter();

        // Assert
        provider.SentToday.Should().Be(0);
    }

    [Fact]
    public void HasDailyCapacity_WithCapacity_ReturnsTrue()
    {
        // Arrange
        var provider = NotificationProvider.Create(
            _tenantId, NotificationChannel.Email, ProviderName.SendGrid,
            "{}", dailyLimit: 100);
        provider.IncrementSentToday(50);

        // Act & Assert
        provider.HasDailyCapacity(10).Should().BeTrue();
        provider.HasDailyCapacity(50).Should().BeTrue();
        provider.HasDailyCapacity(51).Should().BeFalse();
    }
}
