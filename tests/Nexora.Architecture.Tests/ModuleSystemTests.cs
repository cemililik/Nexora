using Nexora.SharedKernel.Abstractions.Modules;

namespace Nexora.Architecture.Tests;

public sealed class ModuleSystemTests
{
    // Static initializer to ensure all module assemblies are loaded before reflection
    static ModuleSystemTests()
    {
        // Trigger lazy-loaded assemblies by referencing each module type
        _ = typeof(Modules.Identity.IdentityModule).Assembly;
        _ = typeof(Modules.Contacts.ContactsModule).Assembly;
        _ = typeof(Modules.Documents.DocumentsModule).Assembly;
        _ = typeof(Modules.Notifications.NotificationsModule).Assembly;
        _ = typeof(Modules.Reporting.ReportingModule).Assembly;
    }
    [Fact]
    public void IdentityModule_ShouldImplementIModule()
    {
        var module = new Modules.Identity.IdentityModule();

        module.Should().BeAssignableTo<IModule>();
    }

    [Fact]
    public void IdentityModule_ShouldHaveCorrectName()
    {
        var module = new Modules.Identity.IdentityModule();

        module.Name.Should().Be("identity");
    }

    [Fact]
    public void IdentityModule_ShouldHaveNoDependencies()
    {
        var module = new Modules.Identity.IdentityModule();

        module.Dependencies.Should().BeEmpty(
            "Identity is the foundational module with no dependencies");
    }

    [Fact]
    public void IdentityModule_ShouldHaveVersion()
    {
        var module = new Modules.Identity.IdentityModule();

        module.Version.Should().NotBeNullOrEmpty();
        module.Version.Should().MatchRegex(@"^\d+\.\d+\.\d+$",
            "Version should follow SemVer format");
    }

    [Fact]
    public void AllModules_ShouldBeDiscoverable()
    {
        var moduleTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IModule).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
            .ToList();

        moduleTypes.Should().NotBeEmpty("At least one module should be discoverable");
        moduleTypes.Should().Contain(t => t.Name == "IdentityModule");
    }

    [Fact]
    public void IdentityModule_OnUninstall_ShouldThrow()
    {
        var module = new Modules.Identity.IdentityModule();
        var context = new TenantInstallContext("t1", "tenant_t1", null);

        var act = () => module.OnUninstallAsync(context, CancellationToken.None);

        act.Should().ThrowAsync<InvalidOperationException>();
    }
}
