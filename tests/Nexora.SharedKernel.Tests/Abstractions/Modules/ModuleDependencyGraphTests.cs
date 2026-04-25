using Nexora.SharedKernel.Abstractions.Modules;
using NSubstitute;

namespace Nexora.SharedKernel.Tests.Abstractions.Modules;

public sealed class ModuleDependencyGraphTests
{
    private static IModule Module(string name, params string[] deps)
    {
        var m = Substitute.For<IModule>();
        m.Name.Returns(name);
        m.Dependencies.Returns(deps);
        return m;
    }

    [Fact]
    public void OrderByDependencies_LeafFirstRootLast()
    {
        var identity = Module("identity");
        var contacts = Module("contacts", "identity");
        var crm = Module("crm", "contacts");

        var ordered = ModuleDependencyGraph.OrderByDependencies([crm, contacts, identity]);

        ordered.Select(m => m.Name).Should().Equal("identity", "contacts", "crm");
    }

    [Fact]
    public void OrderByDependencies_Cycle_Throws()
    {
        var a = Module("a", "b");
        var b = Module("b", "a");

        var act = () => ModuleDependencyGraph.OrderByDependencies([a, b]);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Cycle*");
    }

    [Fact]
    public void OrderByDependencies_MissingDependency_Throws()
    {
        var crm = Module("crm", "missing-module");
        var act = () => ModuleDependencyGraph.OrderByDependencies([crm]);
        act.Should().Throw<InvalidOperationException>().WithMessage("*missing-module*");
    }

    [Fact]
    public void FindInstalledDependents_DirectAndTransitive()
    {
        // identity ← contacts ← crm; uninstalling identity must surface
        // both contacts and crm as dependents.
        var identity = Module("identity");
        var contacts = Module("contacts", "identity");
        var crm = Module("crm", "contacts");

        var dependents = ModuleDependencyGraph.FindInstalledDependents(
            "identity", [identity, contacts, crm]);

        dependents.Select(m => m.Name).Should().Equal("contacts", "crm");
    }

    [Fact]
    public void FindInstalledDependents_NoDependents_ReturnsEmpty()
    {
        var identity = Module("identity");
        var crm = Module("crm");

        var dependents = ModuleDependencyGraph.FindInstalledDependents(
            "crm", [identity, crm]);

        dependents.Should().BeEmpty();
    }

    [Fact]
    public void ReverseUninstallOrder_DependentsFirst_TargetLast()
    {
        var identity = Module("identity");
        var contacts = Module("contacts", "identity");
        var crm = Module("crm", "contacts");

        var sequence = ModuleDependencyGraph.ReverseUninstallOrder(
            "identity", [identity, contacts, crm]);

        // Cascade order: leaf-most dependent first (crm), then contacts,
        // then the target (identity) last so the root drops with no
        // surviving dependent.
        sequence.Select(m => m.Name).Should().Equal("crm", "contacts", "identity");
    }

    [Fact]
    public void ReverseUninstallOrder_TargetMissing_Throws()
    {
        var identity = Module("identity");
        var act = () => ModuleDependencyGraph.ReverseUninstallOrder("crm", [identity]);
        act.Should().Throw<InvalidOperationException>().WithMessage("*crm*");
    }
}
