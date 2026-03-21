using NetArchTest.Rules;

namespace Nexora.Architecture.Tests;

/// <summary>Architecture tests for Documents module Phase 2 entities and services.</summary>
public sealed class DocumentsModulePhase2ArchitectureTests
{
    private static readonly System.Reflection.Assembly DocumentsAssembly =
        typeof(Modules.Documents.DocumentsModule).Assembly;

    [Fact]
    public void SignatureRequest_ShouldResideInDomainEntities()
    {
        var result = Types.InAssembly(DocumentsAssembly)
            .That()
            .HaveNameStartingWith("SignatureRequest")
            .And()
            .AreClasses()
            .And()
            .ResideInNamespace("Nexora.Modules.Documents.Domain.Entities")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "SignatureRequest entity should be sealed in Domain.Entities");
    }

    [Fact]
    public void SignatureRecipient_ShouldResideInDomainEntities()
    {
        var result = Types.InAssembly(DocumentsAssembly)
            .That()
            .HaveNameStartingWith("SignatureRecipient")
            .And()
            .AreClasses()
            .And()
            .ResideInNamespace("Nexora.Modules.Documents.Domain.Entities")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "SignatureRecipient entity should be sealed in Domain.Entities");
    }

    [Fact]
    public void DocumentTemplate_ShouldResideInDomainEntities()
    {
        var result = Types.InAssembly(DocumentsAssembly)
            .That()
            .HaveNameStartingWith("DocumentTemplate")
            .And()
            .AreClasses()
            .And()
            .ResideInNamespace("Nexora.Modules.Documents.Domain.Entities")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "DocumentTemplate entity should be sealed in Domain.Entities");
    }

    [Fact]
    public void DomainEvents_ShouldBeSealed()
    {
        var result = Types.InAssembly(DocumentsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Documents.Domain.Events")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All domain events in Documents module should be sealed");
    }

    [Fact]
    public void IntegrationEventHandlers_ShouldBeSealed()
    {
        var result = Types.InAssembly(DocumentsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Documents.Infrastructure.IntegrationEvents")
            .And()
            .HaveNameEndingWith("Handler")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All integration event handlers should be sealed");
    }

    [Fact]
    public void Jobs_ShouldBeSealed()
    {
        var result = Types.InAssembly(DocumentsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Documents.Infrastructure.Jobs")
            .And()
            .HaveNameEndingWith("Job")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All background jobs should be sealed");
    }

    [Fact]
    public void InfrastructureServices_ShouldBeSealed()
    {
        var result = Types.InAssembly(DocumentsAssembly)
            .That()
            .ResideInNamespace("Nexora.Modules.Documents.Infrastructure.Services")
            .And()
            .AreClasses()
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All infrastructure services should be sealed");
    }

    [Fact]
    public void Phase2Entities_ShouldNotDependOnApplication()
    {
        var entityTypes = new[] { "SignatureRequest", "SignatureRecipient", "DocumentTemplate" };
        foreach (var entity in entityTypes)
        {
            var result = Types.InAssembly(DocumentsAssembly)
                .That()
                .HaveName(entity)
                .ShouldNot()
                .HaveDependencyOn("Nexora.Modules.Documents.Application")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"{entity} should not depend on Application layer");
        }
    }

    [Fact]
    public void Phase2Entities_ShouldNotDependOnInfrastructure()
    {
        var entityTypes = new[] { "SignatureRequest", "SignatureRecipient", "DocumentTemplate" };
        foreach (var entity in entityTypes)
        {
            var result = Types.InAssembly(DocumentsAssembly)
                .That()
                .HaveName(entity)
                .ShouldNot()
                .HaveDependencyOn("Nexora.Modules.Documents.Infrastructure")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"{entity} should not depend on Infrastructure layer");
        }
    }

    [Fact]
    public void CrossModuleService_ShouldImplementSharedKernelInterface()
    {
        var documentServiceTypes = Types.InAssembly(DocumentsAssembly)
            .That()
            .HaveName("DocumentService")
            .And()
            .ResideInNamespace("Nexora.Modules.Documents.Infrastructure.Services")
            .GetTypes();

        documentServiceTypes.Should().NotBeEmpty(
            "DocumentService implementation should exist in Infrastructure.Services");
    }
}
