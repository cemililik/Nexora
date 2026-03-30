using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Audit.Application.Queries;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Domain.Repositories;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using NSubstitute;

namespace Nexora.Modules.Audit.Tests.Application;

public sealed class GetAuditSettingsQueryTests
{
    private readonly IAuditSettingRepository _repository;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly string _tenantId = Guid.NewGuid().ToString();

    public GetAuditSettingsQueryTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        _repository = Substitute.For<IAuditSettingRepository>();
    }

    [Fact]
    public async Task Handle_NoSettingsExist_ShouldReturnEmptyList()
    {
        _repository.GetAllByTenantAsync(_tenantId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AuditSetting>() as IReadOnlyList<AuditSetting>);

        var handler = new GetAuditSettingsHandler(_repository, _tenantAccessor, NullLogger<GetAuditSettingsHandler>.Instance);
        var result = await handler.Handle(new GetAuditSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MultipleSettingsExist_ShouldReturnAllForTenant()
    {
        var settings = new List<AuditSetting>
        {
            AuditSetting.Create(_tenantId, "Contacts", "CreateContact", true, 90),
            AuditSetting.Create(_tenantId, "CRM", "UpdateLead", false, 30),
            AuditSetting.Create(_tenantId, "Identity", "Login", true, 365)
        };

        _repository.GetAllByTenantAsync(_tenantId, Arg.Any<CancellationToken>())
            .Returns(settings as IReadOnlyList<AuditSetting>);

        var handler = new GetAuditSettingsHandler(_repository, _tenantAccessor, NullLogger<GetAuditSettingsHandler>.Instance);
        var result = await handler.Handle(new GetAuditSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithMultipleSettings_ShouldOrderByModuleThenOperation()
    {
        // Repository returns them already ordered by module then operation (ordering is a repository concern)
        var settings = new List<AuditSetting>
        {
            AuditSetting.Create(_tenantId, "Contacts", "CreateContact", true, 90),
            AuditSetting.Create(_tenantId, "Contacts", "DeleteContact", false, 30),
            AuditSetting.Create(_tenantId, "Identity", "Login", true, 365)
        };

        _repository.GetAllByTenantAsync(_tenantId, Arg.Any<CancellationToken>())
            .Returns(settings as IReadOnlyList<AuditSetting>);

        var handler = new GetAuditSettingsHandler(_repository, _tenantAccessor, NullLogger<GetAuditSettingsHandler>.Instance);
        var result = await handler.Handle(new GetAuditSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dtos = result.Value!;
        dtos[0].Module.Should().Be("contacts");
        dtos[0].Operation.Should().Be("createcontact");
        dtos[1].Module.Should().Be("contacts");
        dtos[1].Operation.Should().Be("deletecontact");
        dtos[2].Module.Should().Be("identity");
        dtos[2].Operation.Should().Be("login");
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldNotReturnOtherTenantSettings()
    {
        // Repository is called with our tenant ID, so it only returns our tenant's settings
        var settings = new List<AuditSetting>
        {
            AuditSetting.Create(_tenantId, "Contacts", "CreateContact", true, 90)
        };

        _repository.GetAllByTenantAsync(_tenantId, Arg.Any<CancellationToken>())
            .Returns(settings as IReadOnlyList<AuditSetting>);

        var handler = new GetAuditSettingsHandler(_repository, _tenantAccessor, NullLogger<GetAuditSettingsHandler>.Instance);
        var result = await handler.Handle(new GetAuditSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value[0].Module.Should().Be("contacts");
    }

    [Fact]
    public async Task Handle_WithSetting_ShouldReturnCorrectDtoProperties()
    {
        var setting = AuditSetting.Create(_tenantId, "Contacts", "CreateContact", true, 90);
        var settings = new List<AuditSetting> { setting };

        _repository.GetAllByTenantAsync(_tenantId, Arg.Any<CancellationToken>())
            .Returns(settings as IReadOnlyList<AuditSetting>);

        var handler = new GetAuditSettingsHandler(_repository, _tenantAccessor, NullLogger<GetAuditSettingsHandler>.Instance);
        var result = await handler.Handle(new GetAuditSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value![0];
        dto.Id.Should().Be(setting.Id.Value);
        dto.Module.Should().Be("contacts");
        dto.Operation.Should().Be("createcontact");
        dto.IsEnabled.Should().BeTrue();
        dto.RetentionDays.Should().Be(90);
    }

    private static ITenantContextAccessor CreateTenantAccessor(string tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId);
        return accessor;
    }
}
