using NetArchTest.Rules;

namespace Nexora.Architecture.Tests;

/// <summary>
/// Enforces ADR-0025 boundary: the tenant-default KV store (<c>TenantConfigEntry</c>)
/// and the org-override store (<c>OrgConfigEntry</c>) MUST only be accessed through
/// <c>IConfigurationResolver</c> (or the legacy shim <c>ITenantConfiguration</c>).
/// Module code that reaches into the tables directly bypasses the three-tier
/// precedence and the compliance cap check.
/// </summary>
public sealed class ConfigurationResolverBoundaryTests
{
    private const string SharedKernelAssembly = "Nexora.SharedKernel";
    private const string InfrastructureAssembly = "Nexora.Infrastructure";
    private const string ConfigurationEntitiesNamespace = "Nexora.Infrastructure.Configuration";

    /// <summary>
    /// Any type outside the infrastructure configuration namespace must go through
    /// <c>IConfigurationResolver</c> / <c>ITenantConfiguration</c> — not
    /// <c>TenantConfigEntry</c>, <c>OrgConfigEntry</c>, or <c>TenantConfigDbContext</c>.
    /// </summary>
    [Fact]
    public void ModuleCode_MustNotReferenceConfigurationEntitiesDirectly()
    {
        // Scan every module assembly (not the Infrastructure project itself) + Host.
        var offenders = new List<string>();

        foreach (var (assemblyName, assembly) in EnumerateModuleAssemblies())
        {
            var result = Types.InAssembly(assembly)
                .That()
                .AreNotAbstract()
                .Should()
                .NotHaveDependencyOnAny(
                    $"{ConfigurationEntitiesNamespace}.TenantConfigEntry",
                    $"{ConfigurationEntitiesNamespace}.OrgConfigEntry",
                    $"{ConfigurationEntitiesNamespace}.TenantConfigDbContext",
                    $"{ConfigurationEntitiesNamespace}.CompliancePolicyAuditEntry")
                .GetResult();

            if (!result.IsSuccessful)
            {
                offenders.AddRange((result.FailingTypeNames ?? [])
                    .Select(t => $"{assemblyName}: {t}"));
            }
        }

        offenders.Should().BeEmpty(
            "module code must go through IConfigurationResolver / ITenantConfiguration rather than reaching into the config tables directly — see ADR-0025 §Implementation notes.");
    }

    private static IEnumerable<(string Name, System.Reflection.Assembly Assembly)> EnumerateModuleAssemblies()
    {
        // All six modules + Host — these are the assemblies that could legitimately
        // need configuration values and must be forced through the resolver rather
        // than reaching into the DbContext or entity types directly.
        var candidates = new[]
        {
            typeof(Nexora.Modules.Identity.IdentityModule).Assembly,
            typeof(Nexora.Modules.Contacts.ContactsModule).Assembly,
            typeof(Nexora.Modules.Documents.DocumentsModule).Assembly,
            typeof(Nexora.Modules.Notifications.NotificationsModule).Assembly,
            typeof(Nexora.Modules.Reporting.ReportingModule).Assembly,
            typeof(Nexora.Modules.Audit.AuditModule).Assembly,
            typeof(Nexora.Host.DevelopmentSeed).Assembly,
        };

        foreach (var asm in candidates)
            yield return (asm.GetName().Name ?? "<unknown>", asm);
    }
}
