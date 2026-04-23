using Nexora.Infrastructure.Authorization;
using Nexora.SharedKernel.Authorization;

namespace Nexora.Infrastructure.Tests.Authorization;

public sealed class InMemoryPermissionRegistryTests
{
    [Fact]
    public void Register_NewPermission_IsQueryable()
    {
        var registry = new InMemoryPermissionRegistry();

        var def = registry.Register("crm", "lead", "read", "lockey_crm_lead_read");

        def.Name.Should().Be("crm.lead.read");
        def.Scope.Should().Be(PermissionScope.Tenant);
        registry.IsRegistered("crm.lead.read").Should().BeTrue();
        registry.GetAll().Should().ContainSingle();
    }

    [Fact]
    public void Register_Duplicate_Throws()
    {
        var registry = new InMemoryPermissionRegistry();
        registry.Register("crm", "lead", "read", "lockey_crm_lead_read");

        var act = () => registry.Register("crm", "lead", "read", "lockey_crm_lead_read_2");

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*already registered*");
    }

    [Fact]
    public void GetByModule_ReturnsOnlyThatModule()
    {
        var registry = new InMemoryPermissionRegistry();
        registry.Register("crm", "lead", "read", "k1");
        registry.Register("crm", "pipeline", "read", "k2");
        registry.Register("finance", "invoice", "read", "k3");

        var crm = registry.GetByModule("crm");

        crm.Should().HaveCount(2);
        crm.Should().OnlyContain(d => d.Module == "crm");
    }

    [Fact]
    public void GetByScope_FiltersCorrectly()
    {
        var registry = new InMemoryPermissionRegistry();
        registry.Register("identity", "tenants", "read", "k1", PermissionScope.Platform);
        registry.Register("identity", "users", "read", "k2", PermissionScope.Tenant);
        registry.Register("identity", "users", "write", "k3", PermissionScope.Tenant);

        registry.GetByScope(PermissionScope.Platform).Should().ContainSingle();
        registry.GetByScope(PermissionScope.Tenant).Should().HaveCount(2);
    }

    [Fact]
    public void Register_PreservesOrder()
    {
        var registry = new InMemoryPermissionRegistry();
        registry.Register("a", "r", "x", "k1");
        registry.Register("b", "r", "x", "k2");
        registry.Register("c", "r", "x", "k3");

        registry.GetAll().Select(d => d.Module)
            .Should().ContainInOrder("a", "b", "c");
    }

    [Theory]
    [InlineData("", "r", "a", "k")]
    [InlineData(" ", "r", "a", "k")]
    [InlineData("m", "", "a", "k")]
    [InlineData("m", "r", "", "k")]
    [InlineData("m", "r", "a", "")]
    public void Register_WithBlankPart_Throws(string module, string resource, string action, string key)
    {
        var registry = new InMemoryPermissionRegistry();
        var act = () => registry.Register(module, resource, action, key);
        act.Should().Throw<ArgumentException>();
    }
}
