using NetArchTest.Rules;

namespace Nexora.Architecture.Tests;

public sealed class ModuleBoundaryTests
{
    private const string SharedKernelNamespace = "Nexora.SharedKernel";
    private const string InfrastructureNamespace = "Nexora.Infrastructure";
    private const string IdentityNamespace = "Nexora.Modules.Identity";
    private const string ContactsNamespace = "Nexora.Modules.Contacts";
    private const string DocumentsNamespace = "Nexora.Modules.Documents";
    private const string NotificationsNamespace = "Nexora.Modules.Notifications";
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
        var assembly = typeof(SharedKernel.Domain.Base.Entity<>).Assembly;

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(IdentityNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("SharedKernel must not depend on Identity module");

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(ContactsNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("SharedKernel must not depend on Contacts module");

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(DocumentsNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("SharedKernel must not depend on Documents module");

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(NotificationsNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("SharedKernel must not depend on Notifications module");
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
        var assembly = typeof(Infrastructure.InfrastructureServiceRegistration).Assembly;

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(IdentityNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("Infrastructure must not depend on Identity module");

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(ContactsNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("Infrastructure must not depend on Contacts module");

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(DocumentsNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("Infrastructure must not depend on Documents module");

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(NotificationsNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("Infrastructure must not depend on Notifications module");
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
        var assembly = typeof(Modules.Identity.IdentityModule).Assembly;

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(ContactsNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("Identity module must not depend on Contacts module");

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.CRM")
            .GetResult()
            .IsSuccessful.Should().BeTrue("Identity module must not depend on CRM module");

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(DocumentsNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("Identity module must not depend on Documents module");

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(NotificationsNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("Identity module must not depend on Notifications module");
    }

    [Fact]
    public void ContactsModule_ShouldNotDependOnOtherModules()
    {
        var assembly = typeof(Modules.Contacts.ContactsModule).Assembly;

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(IdentityNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("Contacts module must not depend on Identity module");

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.CRM")
            .GetResult()
            .IsSuccessful.Should().BeTrue("Contacts module must not depend on CRM module");

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(DocumentsNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("Contacts module must not depend on Documents module");

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(NotificationsNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("Contacts module must not depend on Notifications module");
    }

    [Fact]
    public void DocumentsModule_ShouldNotDependOnOtherModules()
    {
        // Arrange
        var assembly = typeof(Modules.Documents.DocumentsModule).Assembly;

        // Act & Assert
        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(IdentityNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("Documents module must not depend on Identity module");

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(ContactsNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("Documents module must not depend on Contacts module");

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.CRM")
            .GetResult()
            .IsSuccessful.Should().BeTrue("Documents module must not depend on CRM module");

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(NotificationsNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("Documents module must not depend on Notifications module");
    }

    [Fact]
    public void NotificationsModule_ShouldNotDependOnOtherModules()
    {
        var assembly = typeof(Modules.Notifications.NotificationsModule).Assembly;

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(IdentityNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("Notifications module must not depend on Identity module");

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(ContactsNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("Notifications module must not depend on Contacts module");

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(DocumentsNamespace)
            .GetResult()
            .IsSuccessful.Should().BeTrue("Notifications module must not depend on Documents module");

        Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn("Nexora.Modules.CRM")
            .GetResult()
            .IsSuccessful.Should().BeTrue("Notifications module must not depend on CRM module");
    }
}
