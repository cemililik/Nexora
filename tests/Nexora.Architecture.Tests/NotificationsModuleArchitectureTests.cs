using NetArchTest.Rules;

namespace Nexora.Architecture.Tests;

/// <summary>Architecture tests for the Notifications module's internal layer dependencies.</summary>
public sealed class NotificationsModuleArchitectureTests
{
    private static readonly System.Reflection.Assembly NotificationsAssembly =
        typeof(Modules.Notifications.NotificationsModule).Assembly;

    [Fact]
    public void Domain_ShouldNotDependOnApplication()
    {
        var result = Types.InAssembly(NotificationsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Notifications.Domain")
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.Notifications.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain layer must not depend on Application layer");
    }

    [Fact]
    public void Domain_ShouldNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(NotificationsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Notifications.Domain")
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.Notifications.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain layer must not depend on Infrastructure layer");
    }

    [Fact]
    public void Domain_ShouldNotDependOnApi()
    {
        var result = Types.InAssembly(NotificationsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Notifications.Domain")
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.Notifications.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain layer must not depend on Api layer");
    }

    [Fact]
    public void Application_ShouldNotDependOnApi()
    {
        var result = Types.InAssembly(NotificationsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Notifications.Application")
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.Notifications.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Application layer must not depend on Api layer");
    }

    [Fact]
    public void Commands_ShouldBeSealed()
    {
        var result = Types.InAssembly(NotificationsAssembly)
            .That()
            .HaveNameEndingWith("Command")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All commands in Notifications module should be sealed");
    }

    [Fact]
    public void Queries_ShouldBeSealed()
    {
        var result = Types.InAssembly(NotificationsAssembly)
            .That()
            .HaveNameEndingWith("Query")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All queries in Notifications module should be sealed");
    }

    [Fact]
    public void Handlers_ShouldBeSealed()
    {
        var result = Types.InAssembly(NotificationsAssembly)
            .That()
            .HaveNameEndingWith("Handler")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All handlers in Notifications module should be sealed");
    }

    [Fact]
    public void Validators_ShouldBeSealed()
    {
        var result = Types.InAssembly(NotificationsAssembly)
            .That()
            .HaveNameEndingWith("Validator")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All validators in Notifications module should be sealed");
    }

    [Fact]
    public void EfConfigurations_ShouldBeSealed()
    {
        var result = Types.InAssembly(NotificationsAssembly)
            .That()
            .HaveNameEndingWith("Configuration")
            .And()
            .ResideInNamespace("Nexora.Modules.Notifications.Infrastructure.Configurations")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All EF configurations in Notifications module should be sealed");
    }

    [Fact]
    public void DomainEntities_ShouldExist()
    {
        var entityTypes = Types.InAssembly(NotificationsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Notifications.Domain.Entities")
            .GetTypes();

        entityTypes.Should().NotBeEmpty("Notifications module should have domain entities");
    }
}
