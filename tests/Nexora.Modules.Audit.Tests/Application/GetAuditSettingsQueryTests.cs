using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Audit.Application.Queries;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Audit.Tests.Application;

public sealed class GetAuditSettingsQueryTests : IDisposable
{
    private readonly AuditDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly string _tenantId = Guid.NewGuid().ToString();

    public GetAuditSettingsQueryTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);

        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AuditDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_NoSettingsExist_ShouldReturnEmptyList()
    {
        var handler = new GetAuditSettingsHandler(_dbContext, _tenantAccessor);
        var result = await handler.Handle(new GetAuditSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MultipleSettingsExist_ShouldReturnAllForTenant()
    {
        _dbContext.AuditSettings.Add(AuditSetting.Create(_tenantId, "Contacts", "CreateContact", true, 90));
        _dbContext.AuditSettings.Add(AuditSetting.Create(_tenantId, "CRM", "UpdateLead", false, 30));
        _dbContext.AuditSettings.Add(AuditSetting.Create(_tenantId, "Identity", "Login", true, 365));
        await _dbContext.SaveChangesAsync();

        var handler = new GetAuditSettingsHandler(_dbContext, _tenantAccessor);
        var result = await handler.Handle(new GetAuditSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_ShouldOrderByModuleThenOperation()
    {
        _dbContext.AuditSettings.Add(AuditSetting.Create(_tenantId, "Identity", "Login", true, 365));
        _dbContext.AuditSettings.Add(AuditSetting.Create(_tenantId, "Contacts", "DeleteContact", false, 30));
        _dbContext.AuditSettings.Add(AuditSetting.Create(_tenantId, "Contacts", "CreateContact", true, 90));
        await _dbContext.SaveChangesAsync();

        var handler = new GetAuditSettingsHandler(_dbContext, _tenantAccessor);
        var result = await handler.Handle(new GetAuditSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var settings = result.Value!;
        settings[0].Module.Should().Be("Contacts");
        settings[0].Operation.Should().Be("CreateContact");
        settings[1].Module.Should().Be("Contacts");
        settings[1].Operation.Should().Be("DeleteContact");
        settings[2].Module.Should().Be("Identity");
        settings[2].Operation.Should().Be("Login");
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldNotReturnOtherTenantSettings()
    {
        _dbContext.AuditSettings.Add(AuditSetting.Create(_tenantId, "Contacts", "CreateContact", true, 90));
        _dbContext.AuditSettings.Add(AuditSetting.Create("other-tenant", "CRM", "UpdateLead", false, 30));
        await _dbContext.SaveChangesAsync();

        var handler = new GetAuditSettingsHandler(_dbContext, _tenantAccessor);
        var result = await handler.Handle(new GetAuditSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value[0].Module.Should().Be("Contacts");
    }

    [Fact]
    public async Task Handle_ShouldReturnCorrectDtoProperties()
    {
        var setting = AuditSetting.Create(_tenantId, "Contacts", "CreateContact", true, 90);
        _dbContext.AuditSettings.Add(setting);
        await _dbContext.SaveChangesAsync();

        var handler = new GetAuditSettingsHandler(_dbContext, _tenantAccessor);
        var result = await handler.Handle(new GetAuditSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value![0];
        dto.Id.Should().Be(setting.Id.Value);
        dto.Module.Should().Be("Contacts");
        dto.Operation.Should().Be("CreateContact");
        dto.IsEnabled.Should().BeTrue();
        dto.RetentionDays.Should().Be(90);
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(string tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId);
        return accessor;
    }
}
