using NetArchTest.Rules;

namespace Nexora.Architecture.Tests;

/// <summary>Architecture tests for the Contacts module's internal layer dependencies.</summary>
public sealed class ContactsModuleArchitectureTests
{
    private static readonly System.Reflection.Assembly ContactsAssembly =
        typeof(Modules.Contacts.ContactsModule).Assembly;

    [Fact]
    public void Domain_ShouldNotDependOnApplication()
    {
        // Arrange
        var result = Types.InAssembly(ContactsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Contacts.Domain")
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.Contacts.Application")
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer must not depend on Application layer");
    }

    [Fact]
    public void Domain_ShouldNotDependOnInfrastructure()
    {
        // Arrange
        var result = Types.InAssembly(ContactsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Contacts.Domain")
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.Contacts.Infrastructure")
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer must not depend on Infrastructure layer");
    }

    [Fact]
    public void Domain_ShouldNotDependOnApi()
    {
        // Arrange
        var result = Types.InAssembly(ContactsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Contacts.Domain")
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.Contacts.Api")
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer must not depend on Api layer");
    }

    [Fact]
    public void Application_ShouldNotDependOnApi()
    {
        // Arrange
        var result = Types.InAssembly(ContactsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Contacts.Application")
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.Contacts.Api")
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "Application layer must not depend on Api layer");
    }

    [Fact]
    public void Commands_ShouldBeSealed()
    {
        // Arrange
        var result = Types.InAssembly(ContactsAssembly)
            .That()
            .HaveNameEndingWith("Command")
            .Should()
            .BeSealed()
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "All commands in Contacts module should be sealed");
    }

    [Fact]
    public void Queries_ShouldBeSealed()
    {
        // Arrange
        var result = Types.InAssembly(ContactsAssembly)
            .That()
            .HaveNameEndingWith("Query")
            .Should()
            .BeSealed()
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "All queries in Contacts module should be sealed");
    }

    [Fact]
    public void Handlers_ShouldBeSealed()
    {
        // Arrange
        var result = Types.InAssembly(ContactsAssembly)
            .That()
            .HaveNameEndingWith("Handler")
            .Should()
            .BeSealed()
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "All handlers in Contacts module should be sealed");
    }

    [Fact]
    public void Validators_ShouldBeSealed()
    {
        // Arrange
        var result = Types.InAssembly(ContactsAssembly)
            .That()
            .HaveNameEndingWith("Validator")
            .Should()
            .BeSealed()
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "All validators in Contacts module should be sealed");
    }

    [Fact]
    public void EfConfigurations_ShouldBeSealed()
    {
        // Arrange
        var result = Types.InAssembly(ContactsAssembly)
            .That()
            .HaveNameEndingWith("Configuration")
            .And()
            .ResideInNamespace("Nexora.Modules.Contacts.Infrastructure.Configurations")
            .Should()
            .BeSealed()
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "All EF configurations in Contacts module should be sealed");
    }

    [Fact]
    public void DomainEntities_ShouldExist()
    {
        // Arrange
        var entityTypes = Types.InAssembly(ContactsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Contacts.Domain.Entities")
            .GetTypes();

        // Act & Assert
        entityTypes.Should().NotBeEmpty("Contacts module should have domain entities");
    }
}
