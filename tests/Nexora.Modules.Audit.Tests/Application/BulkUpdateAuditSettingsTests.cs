using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Audit.Application.Commands;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Domain.Repositories;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Caching;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using NSubstitute;

namespace Nexora.Modules.Audit.Tests.Application;

public sealed class BulkUpdateAuditSettingsTests
{
    private readonly IAuditSettingRepository _repository;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly ICacheService _cacheService;
    private readonly string _tenantId = Guid.NewGuid().ToString();
    private readonly List<AuditSetting> _addedSettings = new();

    public BulkUpdateAuditSettingsTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        _cacheService = Substitute.For<ICacheService>();
        _repository = Substitute.For<IAuditSettingRepository>();

        _repository.When(r => r.Add(Arg.Any<AuditSetting>()))
            .Do(ci => _addedSettings.Add(ci.ArgAt<AuditSetting>(0)));
    }

    [Fact]
    public async Task Handle_NewSettings_ShouldCreateAll()
    {
        _repository.GetAllByTenantAsync(_tenantId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AuditSetting>() as IReadOnlyList<AuditSetting>);

        var handler = new BulkUpdateAuditSettingsHandler(
            _repository, _tenantAccessor, _cacheService,
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

        _repository.Received(3).Add(Arg.Any<AuditSetting>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingSettings_ShouldUpdateAll()
    {
        var existingContactsSetting = AuditSetting.Create(_tenantId, "Contacts", "CreateContact", true, 90);
        var existingCrmSetting = AuditSetting.Create(_tenantId, "CRM", "UpdateLead", false, 30);

        _repository.GetAllByTenantAsync(_tenantId, Arg.Any<CancellationToken>())
            .Returns(new List<AuditSetting> { existingContactsSetting, existingCrmSetting } as IReadOnlyList<AuditSetting>);

        var handler = new BulkUpdateAuditSettingsHandler(
            _repository, _tenantAccessor, _cacheService,
            NullLogger<BulkUpdateAuditSettingsHandler>.Instance);

        var command = new BulkUpdateAuditSettingsCommand(
        [
            new AuditSettingItem("Contacts", "CreateContact", false, 180),
            new AuditSettingItem("CRM", "UpdateLead", true, 365)
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);

        // Verify updates on existing entities
        existingContactsSetting.IsEnabled.Should().BeFalse();
        existingContactsSetting.RetentionDays.Should().Be(180);

        existingCrmSetting.IsEnabled.Should().BeTrue();
        existingCrmSetting.RetentionDays.Should().Be(365);

        // No new entities added
        _repository.DidNotReceive().Add(Arg.Any<AuditSetting>());
    }

    [Fact]
    public async Task Handle_MixedCreateAndUpdate_ShouldHandleBoth()
    {
        var existingContactsSetting = AuditSetting.Create(_tenantId, "Contacts", "CreateContact", true, 90);

        _repository.GetAllByTenantAsync(_tenantId, Arg.Any<CancellationToken>())
            .Returns(new List<AuditSetting> { existingContactsSetting } as IReadOnlyList<AuditSetting>);

        var handler = new BulkUpdateAuditSettingsHandler(
            _repository, _tenantAccessor, _cacheService,
            NullLogger<BulkUpdateAuditSettingsHandler>.Instance);

        var command = new BulkUpdateAuditSettingsCommand(
        [
            new AuditSettingItem("Contacts", "CreateContact", false, 30),  // update
            new AuditSettingItem("CRM", "NewOp", true, 180)              // create
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);

        // One existing updated, one new added
        _repository.Received(1).Add(Arg.Any<AuditSetting>());
    }

    [Fact]
    public async Task Handle_MultipleSettings_InvalidatesCacheForEachSetting()
    {
        _repository.GetAllByTenantAsync(_tenantId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AuditSetting>() as IReadOnlyList<AuditSetting>);

        var handler = new BulkUpdateAuditSettingsHandler(
            _repository, _tenantAccessor, _cacheService,
            NullLogger<BulkUpdateAuditSettingsHandler>.Instance);

        var command = new BulkUpdateAuditSettingsCommand(
        [
            new AuditSettingItem("Contacts", "CreateContact", true, 90),
            new AuditSettingItem("CRM", "UpdateLead", true, 30)
        ]);

        await handler.Handle(command, CancellationToken.None);

        // Each setting should invalidate 2 cache keys (defaultEnabled true and false variants)
        await _cacheService.Received(1).RemoveAsync(
            "audit:contacts:config:createcontact:1", Arg.Any<CancellationToken>());
        await _cacheService.Received(1).RemoveAsync(
            "audit:contacts:config:createcontact:0", Arg.Any<CancellationToken>());
        await _cacheService.Received(1).RemoveAsync(
            "audit:crm:config:updatelead:1", Arg.Any<CancellationToken>());
        await _cacheService.Received(1).RemoveAsync(
            "audit:crm:config:updatelead:0", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyList_ShouldReturnEmptySuccess()
    {
        _repository.GetAllByTenantAsync(_tenantId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AuditSetting>() as IReadOnlyList<AuditSetting>);

        var handler = new BulkUpdateAuditSettingsHandler(
            _repository, _tenantAccessor, _cacheService,
            NullLogger<BulkUpdateAuditSettingsHandler>.Instance);

        var command = new BulkUpdateAuditSettingsCommand([]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    private static ITenantContextAccessor CreateTenantAccessor(string tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId);
        return accessor;
    }
}
