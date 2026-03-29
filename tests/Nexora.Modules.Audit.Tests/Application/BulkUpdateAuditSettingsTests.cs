using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Audit.Application.Commands;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Caching;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using NSubstitute;

namespace Nexora.Modules.Audit.Tests.Application;

public sealed class BulkUpdateAuditSettingsTests : IDisposable
{
    private readonly AuditDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly ICacheService _cacheService;
    private readonly string _tenantId = Guid.NewGuid().ToString();

    public BulkUpdateAuditSettingsTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        _cacheService = Substitute.For<ICacheService>();

        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AuditDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_NewSettings_ShouldCreateAll()
    {
        var handler = new BulkUpdateAuditSettingsHandler(
            _dbContext, _tenantAccessor, _cacheService,
            NullLogger<BulkUpdateAuditSettingsHandler>.Instance);

        var command = new BulkUpdateAuditSettingsCommand(
        [
            new AuditSettingItem("Contacts", "CreateContact", true, 90),
            new AuditSettingItem("Contacts", "DeleteContact", false, 30),
            new AuditSettingItem("CRM", "UpdateLead", true, 365)
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(3);

        var count = await _dbContext.AuditSettings.CountAsync();
        count.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ExistingSettings_ShouldUpdateAll()
    {
        // Seed existing settings
        _dbContext.AuditSettings.Add(AuditSetting.Create(_tenantId, "Contacts", "CreateContact", true, 90));
        _dbContext.AuditSettings.Add(AuditSetting.Create(_tenantId, "CRM", "UpdateLead", false, 30));
        await _dbContext.SaveChangesAsync();

        var handler = new BulkUpdateAuditSettingsHandler(
            _dbContext, _tenantAccessor, _cacheService,
            NullLogger<BulkUpdateAuditSettingsHandler>.Instance);

        var command = new BulkUpdateAuditSettingsCommand(
        [
            new AuditSettingItem("Contacts", "CreateContact", false, 180),
            new AuditSettingItem("CRM", "UpdateLead", true, 365)
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);

        // Verify updates
        var contactsSetting = await _dbContext.AuditSettings
            .FirstAsync(s => s.Module == "Contacts" && s.Operation == "CreateContact");
        contactsSetting.IsEnabled.Should().BeFalse();
        contactsSetting.RetentionDays.Should().Be(180);

        var crmSetting = await _dbContext.AuditSettings
            .FirstAsync(s => s.Module == "CRM" && s.Operation == "UpdateLead");
        crmSetting.IsEnabled.Should().BeTrue();
        crmSetting.RetentionDays.Should().Be(365);

        // No duplicates
        var totalCount = await _dbContext.AuditSettings.CountAsync();
        totalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_MixedCreateAndUpdate_ShouldHandleBoth()
    {
        // Seed one existing
        _dbContext.AuditSettings.Add(AuditSetting.Create(_tenantId, "Contacts", "CreateContact", true, 90));
        await _dbContext.SaveChangesAsync();

        var handler = new BulkUpdateAuditSettingsHandler(
            _dbContext, _tenantAccessor, _cacheService,
            NullLogger<BulkUpdateAuditSettingsHandler>.Instance);

        var command = new BulkUpdateAuditSettingsCommand(
        [
            new AuditSettingItem("Contacts", "CreateContact", false, 30),  // update
            new AuditSettingItem("CRM", "NewOp", true, 180)              // create
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);

        var totalCount = await _dbContext.AuditSettings.CountAsync();
        totalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_MultipleSettings_InvalidatesCacheForEachSetting()
    {
        var handler = new BulkUpdateAuditSettingsHandler(
            _dbContext, _tenantAccessor, _cacheService,
            NullLogger<BulkUpdateAuditSettingsHandler>.Instance);

        var command = new BulkUpdateAuditSettingsCommand(
        [
            new AuditSettingItem("Contacts", "CreateContact", true, 90),
            new AuditSettingItem("CRM", "UpdateLead", true, 30)
        ]);

        await handler.Handle(command, CancellationToken.None);

        // Each setting should invalidate 2 cache keys (defaultEnabled true and false variants)
        await _cacheService.Received(1).RemoveAsync(
            $"audit:Contacts:{_tenantId}:config:CreateContact:1", Arg.Any<CancellationToken>());
        await _cacheService.Received(1).RemoveAsync(
            $"audit:Contacts:{_tenantId}:config:CreateContact:0", Arg.Any<CancellationToken>());
        await _cacheService.Received(1).RemoveAsync(
            $"audit:CRM:{_tenantId}:config:UpdateLead:1", Arg.Any<CancellationToken>());
        await _cacheService.Received(1).RemoveAsync(
            $"audit:CRM:{_tenantId}:config:UpdateLead:0", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyList_ShouldReturnEmptySuccess()
    {
        var handler = new BulkUpdateAuditSettingsHandler(
            _dbContext, _tenantAccessor, _cacheService,
            NullLogger<BulkUpdateAuditSettingsHandler>.Instance);

        var command = new BulkUpdateAuditSettingsCommand([]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(string tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId);
        return accessor;
    }
}
