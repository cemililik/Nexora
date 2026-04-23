using System.Reflection;
using NetArchTest.Rules;

namespace Nexora.Architecture.Tests;

/// <summary>
/// T-005 boundary: a module's <c>SeedDemoDataAsync</c> implementation must scope
/// all writes to its own DbContext. Reaching into another module's infrastructure
/// types would re-create the central-seeder god-object the framework exists to
/// prevent.
/// </summary>
public sealed class DemoDataSeedingBoundaryTests
{
    private static readonly string[] ModuleRootNamespaces =
    [
        "Nexora.Modules.Identity",
        "Nexora.Modules.Contacts",
        "Nexora.Modules.Documents",
        "Nexora.Modules.Notifications",
        "Nexora.Modules.Reporting",
        "Nexora.Modules.Audit",
    ];

    private static IEnumerable<Assembly> ModuleAssemblies() =>
    [
        typeof(Nexora.Modules.Identity.IdentityModule).Assembly,
        typeof(Nexora.Modules.Contacts.ContactsModule).Assembly,
        typeof(Nexora.Modules.Documents.DocumentsModule).Assembly,
        typeof(Nexora.Modules.Notifications.NotificationsModule).Assembly,
        typeof(Nexora.Modules.Reporting.ReportingModule).Assembly,
        typeof(Nexora.Modules.Audit.AuditModule).Assembly,
    ];

    [Fact]
    public void Module_Assemblies_ShouldNotReferenceEachOthersInfrastructureNamespaces()
    {
        // Sanity: a module assembly must not even *reference* another module's
        // Infrastructure namespace. If T-007 (scenario seeders) ships a module that
        // violates this, this test catches it before the demo-seeder pipeline runs.
        var offenders = new List<string>();

        foreach (var assembly in ModuleAssemblies())
        {
            var ownPrefix = ModuleRootNamespaces.First(ns =>
                assembly.GetName().Name!.StartsWith(ns, StringComparison.Ordinal));
            var forbiddenPrefixes = ModuleRootNamespaces
                .Where(ns => ns != ownPrefix)
                .Select(ns => $"{ns}.Infrastructure")
                .ToArray();

            var result = Types.InAssembly(assembly)
                .That()
                .ResideInNamespaceStartingWith(ownPrefix)
                .Should()
                .NotHaveDependencyOnAny(forbiddenPrefixes)
                .GetResult();

            if (!result.IsSuccessful)
            {
                offenders.AddRange((result.FailingTypeNames ?? [])
                    .Select(t => $"{assembly.GetName().Name}: {t}"));
            }
        }

        offenders.Should().BeEmpty(
            "modules must not reach into another module's Infrastructure namespace — cross-module data access goes through integration events, not direct type references. Offenders:\n" +
            string.Join("\n", offenders));
    }

    [Fact]
    public void IModule_SeedDemoDataAsync_ExistsOnContract()
    {
        // Locks the T-005 contract: if a future refactor renames or removes the
        // method, every downstream demo-seeder breaks silently. The test ensures
        // the method is there by name and signature.
        var method = typeof(Nexora.SharedKernel.Abstractions.Modules.IModule)
            .GetMethod("SeedDemoDataAsync",
                BindingFlags.Instance | BindingFlags.Public);

        method.Should().NotBeNull("IModule.SeedDemoDataAsync is the T-005 contract seam");
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(2);
        parameters[0].ParameterType.FullName.Should()
            .Be("Nexora.SharedKernel.Abstractions.Modules.TenantDemoSeedContext");
        parameters[1].ParameterType.Should().Be(typeof(CancellationToken));
        method.ReturnType.Should().Be(typeof(Task));
    }
}
