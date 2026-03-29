using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Audit.Application.Commands;
using Nexora.Modules.Audit.Application.DTOs;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Caching;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using NSubstitute;

namespace Nexora.Modules.Audit.Tests.Application;

public sealed class UpdateAuditSettingTests : IDisposable
{
    private readonly AuditDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly ICacheService _cacheService;
    private readonly string _tenantId = Guid.NewGuid().ToString();

    public UpdateAuditSettingTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        _cacheService = Substitute.For<ICacheService>();

        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AuditDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_NewSetting_ShouldCreateAndReturnDto()
    {
        var handler = new UpdateAuditSettingHandler(
            _dbContext, _tenantAccessor, _cacheService,
            NullLogger<UpdateAuditSettingHandler>.Instance);

        var command = new UpdateAuditSettingCommand("Contacts", "CreateContact", true, 90);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Module.Should().Be("contacts");
        result.Value.Operation.Should().Be("createcontact");
        result.Value.IsEnabled.Should().BeTrue();
        result.Value.RetentionDays.Should().Be(90);
        result.Value.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_NewSetting_ShouldPersistToDatabase()
    {
        var handler = new UpdateAuditSettingHandler(
            _dbContext, _tenantAccessor, _cacheService,
            NullLogger<UpdateAuditSettingHandler>.Instance);

        await handler.Handle(
            new UpdateAuditSettingCommand("CRM", "UpdateLead", false, 30),
            CancellationToken.None);

        var count = await _dbContext.AuditSettings.CountAsync();
        count.Should().Be(1);

        var persisted = await _dbContext.AuditSettings.FirstAsync();
        persisted.Module.Should().Be("crm");
        persisted.Operation.Should().Be("updatelead");
        persisted.IsEnabled.Should().BeFalse();
        persisted.RetentionDays.Should().Be(30);
        persisted.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task Handle_ExistingSetting_ShouldUpdateInPlace()
    {
        // Seed an existing setting
        var existing = AuditSetting.Create(_tenantId, "Contacts", "CreateContact", true, 90);
        _dbContext.AuditSettings.Add(existing);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateAuditSettingHandler(
            _dbContext, _tenantAccessor, _cacheService,
            NullLogger<UpdateAuditSettingHandler>.Instance);

        var result = await handler.Handle(
            new UpdateAuditSettingCommand("Contacts", "CreateContact", false, 365),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsEnabled.Should().BeFalse();
        result.Value.RetentionDays.Should().Be(365);

        // Verify only one record exists (updated, not duplicated)
        var totalCount = await _dbContext.AuditSettings.CountAsync();
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SettingCreated_InvalidatesBothCacheVariants()
    {
        var handler = new UpdateAuditSettingHandler(
            _dbContext, _tenantAccessor, _cacheService,
            NullLogger<UpdateAuditSettingHandler>.Instance);

        await handler.Handle(
            new UpdateAuditSettingCommand("Contacts", "CreateContact", true, 90),
            CancellationToken.None);

        await _cacheService.Received(1).RemoveAsync(
            $"audit:contacts:{_tenantId}:config:createcontact:1",
            Arg.Any<CancellationToken>());
        await _cacheService.Received(1).RemoveAsync(
            $"audit:contacts:{_tenantId}:config:createcontact:0",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingSetting_ShouldPreserveId()
    {
        var existing = AuditSetting.Create(_tenantId, "Contacts", "DeleteContact", true, 60);
        _dbContext.AuditSettings.Add(existing);
        await _dbContext.SaveChangesAsync();
        var originalId = existing.Id.Value;

        var handler = new UpdateAuditSettingHandler(
            _dbContext, _tenantAccessor, _cacheService,
            NullLogger<UpdateAuditSettingHandler>.Instance);

        var result = await handler.Handle(
            new UpdateAuditSettingCommand("Contacts", "DeleteContact", false, 180),
            CancellationToken.None);

        result.Value!.Id.Should().Be(originalId);
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(string tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId);
        return accessor;
    }
}

public sealed class UpdateAuditSettingValidatorTests
{
    private readonly UpdateAuditSettingValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var command = new UpdateAuditSettingCommand("Contacts", "CreateContact", true, 90);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "CreateContact")]
    [InlineData(null, "CreateContact")]
    [InlineData("Contacts", "")]
    [InlineData("Contacts", null)]
    public void Validate_EmptyModuleOrOperation_ShouldFail(string? module, string? operation)
    {
        var command = new UpdateAuditSettingCommand(module!, operation!, true, 90);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_RetentionDaysNotPositive_ShouldFail(int retentionDays)
    {
        var command = new UpdateAuditSettingCommand("Contacts", "CreateContact", true, retentionDays);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == "lockey_audit_validation_retention_days_must_be_positive");
    }

    [Fact]
    public void Validate_RetentionDaysExceedsMax_ShouldFail()
    {
        var command = new UpdateAuditSettingCommand("Contacts", "CreateContact", true, 3651);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == "lockey_audit_validation_retention_days_max_exceeded");
    }

    [Fact]
    public void Validate_RetentionDaysAtMax_ShouldPass()
    {
        var command = new UpdateAuditSettingCommand("Contacts", "CreateContact", true, 3650);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
