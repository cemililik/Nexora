using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Notifications.Application.Queries;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Application.Queries;

public sealed class GetNotificationTemplatesTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetNotificationTemplatesTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ShouldReturnEmptyList()
    {
        // Arrange
        var handler = new GetNotificationTemplatesHandler(_dbContext, _tenantAccessor);

        // Act
        var result = await handler.Handle(new GetNotificationTemplatesQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithTemplates_ShouldReturnPagedResult()
    {
        // Arrange
        await SeedTemplates(3);
        var handler = new GetNotificationTemplatesHandler(_dbContext, _tenantAccessor);

        // Act
        var result = await handler.Handle(new GetNotificationTemplatesQuery(Page: 1, PageSize: 2), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_FilterByChannel_ShouldFilterCorrectly()
    {
        // Arrange
        await SeedTemplate("email_template", NotificationChannel.Email);
        await SeedTemplate("sms_template", NotificationChannel.Sms);
        var handler = new GetNotificationTemplatesHandler(_dbContext, _tenantAccessor);

        // Act
        var result = await handler.Handle(new GetNotificationTemplatesQuery(Channel: "Email"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Channel.Should().Be("Email");
    }

    [Fact]
    public async Task Handle_FilterByModule_ShouldFilterCorrectly()
    {
        // Arrange
        await SeedTemplate("identity_welcome", NotificationChannel.Email, module: "identity");
        await SeedTemplate("contacts_import", NotificationChannel.Email, module: "contacts");
        var handler = new GetNotificationTemplatesHandler(_dbContext, _tenantAccessor);

        // Act
        var result = await handler.Handle(new GetNotificationTemplatesQuery(Module: "identity"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Module.Should().Be("identity");
    }

    [Fact]
    public async Task Handle_FilterByIsActive_ShouldFilterCorrectly()
    {
        // Arrange
        var active = await SeedTemplate("active_template", NotificationChannel.Email);
        var inactive = await SeedTemplate("inactive_template", NotificationChannel.Sms);
        inactive.Deactivate();
        await _dbContext.SaveChangesAsync();

        var handler = new GetNotificationTemplatesHandler(_dbContext, _tenantAccessor);

        // Act
        var result = await handler.Handle(new GetNotificationTemplatesQuery(IsActive: true), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldOnlyReturnCurrentTenant()
    {
        // Arrange
        await SeedTemplate("own_template", NotificationChannel.Email);
        var otherTenantTemplate = NotificationTemplate.Create(
            Guid.NewGuid(), "other_template", "notifications", NotificationChannel.Email,
            "Other", "Body", TemplateFormat.Html);
        otherTenantTemplate.ClearDomainEvents();
        await _dbContext.NotificationTemplates.AddAsync(otherTenantTemplate);
        await _dbContext.SaveChangesAsync();

        var handler = new GetNotificationTemplatesHandler(_dbContext, _tenantAccessor);

        // Act
        var result = await handler.Handle(new GetNotificationTemplatesQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
    }

    private async Task SeedTemplates(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await SeedTemplate($"template_{i:D2}", NotificationChannel.Email);
        }
    }

    private async Task<NotificationTemplate> SeedTemplate(string code, NotificationChannel channel,
        string module = "notifications")
    {
        var template = NotificationTemplate.Create(
            _tenantId, code, module, channel, $"Subject for {code}", "Body", TemplateFormat.Html);
        template.ClearDomainEvents();
        await _dbContext.NotificationTemplates.AddAsync(template);
        await _dbContext.SaveChangesAsync();
        return template;
    }

    public void Dispose() => _dbContext.Dispose();
}
