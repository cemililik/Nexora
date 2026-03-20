using NetArchTest.Rules;

namespace Nexora.Architecture.Tests;

/// <summary>Architecture tests for the Documents module's internal layer dependencies.</summary>
public sealed class DocumentsModuleArchitectureTests
{
    private static readonly System.Reflection.Assembly DocumentsAssembly =
        typeof(Modules.Documents.DocumentsModule).Assembly;

    [Fact]
    public void Domain_ShouldNotDependOnApplication()
    {
        // Arrange
        var result = Types.InAssembly(DocumentsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Documents.Domain")
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.Documents.Application")
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer must not depend on Application layer");
    }

    [Fact]
    public void Domain_ShouldNotDependOnInfrastructure()
    {
        // Arrange
        var result = Types.InAssembly(DocumentsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Documents.Domain")
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.Documents.Infrastructure")
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer must not depend on Infrastructure layer");
    }

    [Fact]
    public void Domain_ShouldNotDependOnApi()
    {
        // Arrange
        var result = Types.InAssembly(DocumentsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Documents.Domain")
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.Documents.Api")
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer must not depend on Api layer");
    }

    [Fact]
    public void Application_ShouldNotDependOnApi()
    {
        // Arrange
        var result = Types.InAssembly(DocumentsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Documents.Application")
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.Documents.Api")
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "Application layer must not depend on Api layer");
    }

    [Fact]
    public void Commands_ShouldBeSealed()
    {
        // Arrange
        var result = Types.InAssembly(DocumentsAssembly)
            .That()
            .HaveNameEndingWith("Command")
            .Should()
            .BeSealed()
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "All commands in Documents module should be sealed");
    }

    [Fact]
    public void Queries_ShouldBeSealed()
    {
        // Arrange
        var result = Types.InAssembly(DocumentsAssembly)
            .That()
            .HaveNameEndingWith("Query")
            .Should()
            .BeSealed()
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "All queries in Documents module should be sealed");
    }

    [Fact]
    public void Handlers_ShouldBeSealed()
    {
        // Arrange
        var result = Types.InAssembly(DocumentsAssembly)
            .That()
            .HaveNameEndingWith("Handler")
            .Should()
            .BeSealed()
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "All handlers in Documents module should be sealed");
    }

    [Fact]
    public void Validators_ShouldBeSealed()
    {
        // Arrange
        var result = Types.InAssembly(DocumentsAssembly)
            .That()
            .HaveNameEndingWith("Validator")
            .Should()
            .BeSealed()
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "All validators in Documents module should be sealed");
    }

    [Fact]
    public void EfConfigurations_ShouldBeSealed()
    {
        // Arrange
        var result = Types.InAssembly(DocumentsAssembly)
            .That()
            .HaveNameEndingWith("Configuration")
            .And()
            .ResideInNamespace("Nexora.Modules.Documents.Infrastructure.Configurations")
            .Should()
            .BeSealed()
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "All EF configurations in Documents module should be sealed");
    }

    [Fact]
    public void DomainEntities_ShouldExist()
    {
        // Arrange
        var entityTypes = Types.InAssembly(DocumentsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Documents.Domain.Entities")
            .GetTypes();

        // Act & Assert
        entityTypes.Should().NotBeEmpty("Documents module should have domain entities");
    }
}
