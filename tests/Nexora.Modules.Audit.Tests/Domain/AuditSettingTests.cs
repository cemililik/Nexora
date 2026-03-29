using Nexora.Modules.Audit.Domain.Entities;

namespace Nexora.Modules.Audit.Tests.Domain;

public sealed class AuditSettingTests
{
    [Fact]
    public void Create_ValidParameters_ShouldReturnAuditSetting()
    {
        var setting = AuditSetting.Create(
            tenantId: "tenant-1",
            module: "Contacts",
            operation: "CreateContact",
            isEnabled: true,
            retentionDays: 90);

        setting.Should().NotBeNull();
        setting.Id.Value.Should().NotBeEmpty();
        setting.TenantId.Should().Be("tenant-1");
        setting.Module.Should().Be("contacts");
        setting.Operation.Should().Be("createcontact");
        setting.IsEnabled.Should().BeTrue();
        setting.RetentionDays.Should().Be(90);
        setting.UpdatedByUser.Should().BeNull();
    }

    [Fact]
    public void Create_Disabled_ShouldSetIsEnabledFalse()
    {
        var setting = AuditSetting.Create("tenant-1", "CRM", "UpdateLead", false, 30);

        setting.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldGenerateUniqueIds()
    {
        var s1 = AuditSetting.Create("t", "m", "o", true, 90);
        var s2 = AuditSetting.Create("t", "m", "o", true, 90);

        s1.Id.Should().NotBe(s2.Id);
    }

    [Fact]
    public void Create_WildcardOperation_ShouldBeAllowed()
    {
        var setting = AuditSetting.Create("tenant-1", "Contacts", "*", true, 365);

        setting.Operation.Should().Be("*");
    }

    [Fact]
    public void Create_GlobalWildcard_ShouldBeAllowed()
    {
        var setting = AuditSetting.Create("tenant-1", "*", "*", false, 180);

        setting.Module.Should().Be("*");
        setting.Operation.Should().Be("*");
        setting.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Update_ShouldChangeIsEnabledAndRetentionDays()
    {
        var setting = AuditSetting.Create("tenant-1", "Contacts", "CreateContact", true, 90);

        setting.Update(isEnabled: false, retentionDays: 30, updatedBy: "admin@test.com");

        setting.IsEnabled.Should().BeFalse();
        setting.RetentionDays.Should().Be(30);
        setting.UpdatedByUser.Should().Be("admin@test.com");
    }

    [Fact]
    public void Update_ShouldOverwritePreviousValues()
    {
        var setting = AuditSetting.Create("tenant-1", "CRM", "UpdateLead", false, 30);

        setting.Update(true, 365, "first-user");
        setting.Update(false, 180, "second-user");

        setting.IsEnabled.Should().BeFalse();
        setting.RetentionDays.Should().Be(180);
        setting.UpdatedByUser.Should().Be("second-user");
    }

    [Fact]
    public void Update_SameValues_ShouldNotThrow()
    {
        var setting = AuditSetting.Create("tenant-1", "Contacts", "CreateContact", true, 90);

        var act = () => setting.Update(true, 90, "admin");

        act.Should().NotThrow();
        setting.IsEnabled.Should().BeTrue();
        setting.RetentionDays.Should().Be(90);
    }
}
