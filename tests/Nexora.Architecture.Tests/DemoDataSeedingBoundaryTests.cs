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

    /// <summary>
    /// Strict membership check: an assembly belongs to module namespace
    /// <paramref name="moduleNs"/> only when its name equals the namespace
    /// exactly OR begins with the namespace followed by a dot. Plain
    /// <c>StartsWith(ns)</c> would (incorrectly) classify
    /// <c>Nexora.Modules.IdentityAdmin</c> as part of
    /// <c>Nexora.Modules.Identity</c>.
    /// </summary>
    private static bool IsAssemblyInModule(string assemblyName, string moduleNs)
        => assemblyName == moduleNs ||
           assemblyName.StartsWith(moduleNs + ".", StringComparison.Ordinal);

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
            // Use FirstOrDefault + explicit assertion: if a module assembly's
            // namespace is missing from ModuleRootNamespaces, the failure is a
            // diagnostic message naming the assembly (not a generic
            // "Sequence contains no matching element").
            //
            // Match is exact-or-dot-suffix to avoid accidental substring hits
            // (e.g. "Nexora.Modules.IdentityAdmin" must NOT be classified as
            // belonging to "Nexora.Modules.Identity").
            var assemblyName = assembly.GetName().Name!;
            var ownPrefix = ModuleRootNamespaces.FirstOrDefault(ns =>
                IsAssemblyInModule(assemblyName, ns));
            ownPrefix.Should().NotBeNull(
                $"Module assembly '{assemblyName}' has no entry in ModuleRootNamespaces — add it so the boundary scan covers this module.");
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

        // The "default no-op so existing modules compile unchanged" property
        // is contract-load-bearing — without it, every existing module would
        // need a new override on a SharedKernel change. IsAbstract == false
        // is exactly how C# default interface methods are surfaced via
        // reflection.
        method.IsAbstract.Should().BeFalse(
            "SeedDemoDataAsync MUST be a default interface method (DIM) so modules without demo content compile unchanged.");
    }
}
