using Nexora.Infrastructure.Authorization;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Authorization;

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
        _ = typeof(Modules.Audit.AuditModule).Assembly;
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

    /// <summary>
    /// Behavioural counterpart to <c>PermissionRegistryBoundaryTests</c>: instantiates
    /// every module that ships in the host and drives its <c>OnStartupAsync</c> through
    /// a real <see cref="InMemoryPermissionRegistry"/>, then asserts that each module
    /// contributed at least one <see cref="PermissionDefinition"/> whose <c>Module</c>
    /// field matches the module's declared <c>Name</c>. Enforces T-020 / ADR-004 /
    /// permissions.md §3 at runtime (not just by source-text regex).
    /// </summary>
    [Fact]
    public async Task AllModules_OnStartup_ShouldRegisterAtLeastOnePermission()
    {
        var registry = new InMemoryPermissionRegistry();
        var modules = new IModule[]
        {
            new Modules.Identity.IdentityModule(),
            new Modules.Contacts.ContactsModule(),
            new Modules.Documents.DocumentsModule(),
            new Modules.Notifications.NotificationsModule(),
            new Modules.Reporting.ReportingModule(),
            new Modules.Audit.AuditModule(),
        };

        foreach (var module in modules)
        {
            await module.OnStartupAsync(registry, CancellationToken.None);
        }

        var missing = modules
            .Where(m => registry.GetByModule(m.Name).Count == 0)
            .Select(m => m.Name)
            .ToArray();

        missing.Should().BeEmpty(
            "every module must register at least one permission via IPermissionRegistry in OnStartupAsync — see ADR-004 / permissions.md §3. Offenders: " +
            string.Join(", ", missing));
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
