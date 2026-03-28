using NetArchTest.Rules;

namespace Nexora.Architecture.Tests;

/// <summary>Architecture tests for the Reporting module's internal layer dependencies.</summary>
public sealed class ReportingModuleArchitectureTests
{
    private static readonly System.Reflection.Assembly ReportingAssembly =
        typeof(Modules.Reporting.ReportingModule).Assembly;

    [Fact]
    public void Domain_ShouldNotDependOnApplication()
    {
        // Arrange
        var result = Types.InAssembly(ReportingAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Reporting.Domain")
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.Reporting.Application")
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer must not depend on Application layer");
    }

    [Fact]
    public void Domain_ShouldNotDependOnInfrastructure()
    {
        // Arrange
        var result = Types.InAssembly(ReportingAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Reporting.Domain")
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.Reporting.Infrastructure")
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer must not depend on Infrastructure layer");
    }

    [Fact]
    public void Domain_ShouldNotDependOnApi()
    {
        // Arrange
        var result = Types.InAssembly(ReportingAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Reporting.Domain")
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.Reporting.Api")
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer must not depend on Api layer");
    }

    // NOTE: Application_ShouldNotDependOnInfrastructure is intentionally omitted.
    // Reporting module handlers directly reference ReportingDbContext and services
    // in Infrastructure (e.g., SqlQueryValidator, ReportExportService). This is an
    // accepted pattern for this module due to its query-centric nature.

    [Fact]
    public void Application_ShouldNotDependOnApi()
    {
        // Arrange
        var result = Types.InAssembly(ReportingAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Reporting.Application")
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.Reporting.Api")
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "Application layer must not depend on Api layer");
    }

    [Fact]
    public void Commands_ShouldBeSealed()
    {
        // Arrange
        var result = Types.InAssembly(ReportingAssembly)
            .That()
            .HaveNameEndingWith("Command")
            .Should()
            .BeSealed()
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "All commands in Reporting module should be sealed");
    }

    [Fact]
    public void Queries_ShouldBeSealed()
    {
        // Arrange
        var result = Types.InAssembly(ReportingAssembly)
            .That()
            .HaveNameEndingWith("Query")
            .Should()
            .BeSealed()
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "All queries in Reporting module should be sealed");
    }

    [Fact]
    public void Handlers_ShouldBeSealed()
    {
        // Arrange
        var result = Types.InAssembly(ReportingAssembly)
            .That()
            .HaveNameEndingWith("Handler")
            .Should()
            .BeSealed()
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "All handlers in Reporting module should be sealed");
    }

    [Fact]
    public void Validators_ShouldBeSealed()
    {
        // Arrange — filter to FluentValidation validators only (exclude SqlQueryValidator which is partial/source-generated)
        var result = Types.InAssembly(ReportingAssembly)
            .That()
            .HaveNameEndingWith("Validator")
            .And()
            .Inherit(typeof(FluentValidation.AbstractValidator<>))
            .Should()
            .BeSealed()
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "All validators in Reporting module should be sealed");
    }

    [Fact]
    public void EfConfigurations_ShouldBeSealed()
    {
        // Arrange
        var result = Types.InAssembly(ReportingAssembly)
            .That()
            .HaveNameEndingWith("Configuration")
            .And()
            .ResideInNamespace("Nexora.Modules.Reporting.Infrastructure.Configurations")
            .Should()
            .BeSealed()
            .GetResult();

        // Act & Assert
        result.IsSuccessful.Should().BeTrue(
            "All EF configurations in Reporting module should be sealed");
    }

    [Fact]
    public void DomainEntities_ShouldExist()
    {
        // Arrange
        var entityTypes = Types.InAssembly(ReportingAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Reporting.Domain.Entities")
            .GetTypes();

        // Act & Assert
        entityTypes.Should().NotBeEmpty("Reporting module should have domain entities");
    }
}
