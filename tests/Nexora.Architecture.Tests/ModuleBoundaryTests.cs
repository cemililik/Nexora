using NetArchTest.Rules;

namespace Nexora.Architecture.Tests;

public sealed class ModuleBoundaryTests
{
    private const string SharedKernelNamespace = "Nexora.SharedKernel";
    private const string InfrastructureNamespace = "Nexora.Infrastructure";
    private const string IdentityNamespace = "Nexora.Modules.Identity";
    private const string HostNamespace = "Nexora.Host";

    [Fact]
    public void SharedKernel_ShouldNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(typeof(SharedKernel.Domain.Base.Entity<>).Assembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "SharedKernel must not depend on Infrastructure");
    }

    [Fact]
    public void SharedKernel_ShouldNotDependOnModules()
    {
        var result = Types.InAssembly(typeof(SharedKernel.Domain.Base.Entity<>).Assembly)
            .ShouldNot()
            .HaveDependencyOn(IdentityNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "SharedKernel must not depend on any module");
    }

    [Fact]
    public void SharedKernel_ShouldNotDependOnHost()
    {
        var result = Types.InAssembly(typeof(SharedKernel.Domain.Base.Entity<>).Assembly)
            .ShouldNot()
            .HaveDependencyOn(HostNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "SharedKernel must not depend on Host");
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOnModules()
    {
        var result = Types.InAssembly(typeof(Infrastructure.InfrastructureServiceRegistration).Assembly)
            .ShouldNot()
            .HaveDependencyOn(IdentityNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Infrastructure must not depend on any module");
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOnHost()
    {
        var result = Types.InAssembly(typeof(Infrastructure.InfrastructureServiceRegistration).Assembly)
            .ShouldNot()
            .HaveDependencyOn(HostNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Infrastructure must not depend on Host");
    }

    [Fact]
    public void IdentityModule_ShouldNotDependOnOtherModules()
    {
        // Identity is the foundational module — it should not reference other modules.
        // When we add more modules, each module should be tested here.
        var result = Types.InAssembly(typeof(Modules.Identity.IdentityModule).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.CRM")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Identity module must not depend on CRM module");
    }
}
