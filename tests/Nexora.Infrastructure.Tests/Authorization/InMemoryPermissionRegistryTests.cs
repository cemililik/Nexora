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
        registry.Register("crm", "lead", "read", "lockey_test_k1");
        registry.Register("crm", "pipeline", "read", "lockey_test_k2");
        registry.Register("finance", "invoice", "read", "lockey_test_k3");

        var crm = registry.GetByModule("crm");

        crm.Should().HaveCount(2);
        crm.Should().OnlyContain(d => d.Module == "crm");
    }

    [Fact]
    public void GetByScope_FiltersCorrectly()
    {
        var registry = new InMemoryPermissionRegistry();
        registry.Register("identity", "tenants", "read", "lockey_test_k1", PermissionScope.Platform);
        registry.Register("identity", "users", "read", "lockey_test_k2", PermissionScope.Tenant);
        registry.Register("identity", "users", "write", "lockey_test_k3", PermissionScope.Tenant);

        registry.GetByScope(PermissionScope.Platform).Should().ContainSingle();
        registry.GetByScope(PermissionScope.Tenant).Should().HaveCount(2);
    }

    [Fact]
    public void Register_PreservesOrder()
    {
        var registry = new InMemoryPermissionRegistry();
        registry.Register("a", "r", "x", "lockey_test_k1");
        registry.Register("b", "r", "x", "lockey_test_k2");
        registry.Register("c", "r", "x", "lockey_test_k3");

        registry.GetAll().Select(d => d.Module)
            .Should().ContainInOrder("a", "b", "c");
    }

    [Theory]
    // Blank parts — caught by ArgumentException.ThrowIfNullOrWhiteSpace.
    [InlineData("", "r", "a", "lockey_test_k")]
    [InlineData(" ", "r", "a", "lockey_test_k")]
    [InlineData("m", "", "a", "lockey_test_k")]
    [InlineData("m", "r", "", "lockey_test_k")]
    [InlineData("m", "r", "a", "")]
    // Format violations per permissions.md §1 — lowercase, letter-leading segments.
    [InlineData("CRM", "lead", "read", "lockey_test_k")]      // uppercase module
    [InlineData("crm", "Lead", "read", "lockey_test_k")]      // uppercase resource
    [InlineData("crm", "lead", "Read", "lockey_test_k")]      // uppercase action
    [InlineData("1crm", "lead", "read", "lockey_test_k")]     // digit-leading module
    [InlineData("crm", "lead", "read.write", "lockey_test_k")] // dot in action
    [InlineData("crm", "lead", "read write", "lockey_test_k")] // whitespace in action
    // DescriptionKey must carry the lockey_ prefix so FE/BE lookups stay uniform.
    [InlineData("crm", "lead", "read", "k")]
    [InlineData("crm", "lead", "read", "crm_lead_read")]
    public void Register_WithInvalidInput_Throws(string module, string resource, string action, string key)
    {
        var registry = new InMemoryPermissionRegistry();
        var act = () => registry.Register(module, resource, action, key);
        act.Should().Throw<ArgumentException>();
    }
}
