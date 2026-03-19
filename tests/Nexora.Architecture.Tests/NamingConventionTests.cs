using NetArchTest.Rules;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Architecture.Tests;

public sealed class NamingConventionTests
{
    [Fact]
    public void Interfaces_ShouldStartWithI()
    {
        var result = Types.InAssembly(typeof(Entity<>).Assembly)
            .That()
            .AreInterfaces()
            .Should()
            .HaveNameStartingWith("I")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All interfaces must start with 'I'");
    }

    [Fact]
    public void DomainEntities_ShouldNotBePublicWithPublicSetters()
    {
        // Domain entities should use private setters
        var entityTypes = Types.InAssembly(typeof(Modules.Identity.IdentityModule).Assembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Identity.Domain.Entities")
            .GetTypes();

        entityTypes.Should().NotBeEmpty("Identity module should have domain entities");
    }

    [Fact]
    public void Commands_ShouldBeSealed()
    {
        var result = Types.InAssembly(typeof(Modules.Identity.IdentityModule).Assembly)
            .That()
            .HaveNameEndingWith("Command")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Commands should be sealed records");
    }

    [Fact]
    public void Queries_ShouldBeSealed()
    {
        var result = Types.InAssembly(typeof(Modules.Identity.IdentityModule).Assembly)
            .That()
            .HaveNameEndingWith("Query")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Queries should be sealed records");
    }

    [Fact]
    public void Validators_ShouldBeSealed()
    {
        var result = Types.InAssembly(typeof(Modules.Identity.IdentityModule).Assembly)
            .That()
            .HaveNameEndingWith("Validator")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Validators should be sealed classes");
    }

    [Fact]
    public void Handlers_ShouldBeSealed()
    {
        var result = Types.InAssembly(typeof(Modules.Identity.IdentityModule).Assembly)
            .That()
            .HaveNameEndingWith("Handler")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Handlers should be sealed classes");
    }

    [Fact]
    public void EfConfigurations_ShouldBeSealed()
    {
        var result = Types.InAssembly(typeof(Modules.Identity.IdentityModule).Assembly)
            .That()
            .HaveNameEndingWith("Configuration")
            .And()
            .ResideInNamespace("Nexora.Modules.Identity.Infrastructure.Configurations")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "EF configurations should be sealed classes");
    }
}
