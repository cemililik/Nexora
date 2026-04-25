using System.Reflection;
using Nexora.SharedKernel.Abstractions.Audit;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Architecture.Tests;

/// <summary>
/// T-010 (ADR-0026): coverage gate for <see cref="IContactReferenceLocator"/>.
/// Asserts that every module assembly that defines an integration event
/// referencing a <c>ContactId</c> also ships at least one
/// <see cref="IContactReferenceLocator"/> implementation. Without this
/// gate, a Phase-2/3 module could silently emit audit entries with
/// embedded contact PII and the GDPR Article 17 sweep would skip them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why locator-per-module rather than locator-per-event.</b> One
/// locator per module is cheaper to maintain and matches how the scan
/// job dispatches (it groups audit rows by <c>Module</c>). A module
/// emitting 10 ContactId-bearing events still ships a single locator
/// that knows the module's payload shape.
/// </para>
/// <para>
/// <b>Exemptions.</b> <see cref="ExemptModuleNamespaces"/> lists modules
/// whose ContactId-bearing events do not embed PII strings (only IDs).
/// The Contacts module itself is exempt — it owns the contact aggregate
/// and the indexed-path handler covers its rows. The Identity module is
/// exempt because its <c>UserContactLinked/Unlinked</c> events carry only
/// IDs + timestamps; if Identity later adds a PII-bearing event, remove
/// it from the exemption list and a locator implementation must follow.
/// </para>
/// </remarks>
public sealed class ContactReferenceLocatorCoverageTests
{
    /// <summary>
    /// Module-namespace prefixes whose ContactId-bearing events do not
    /// embed PII strings, so a locator is not required. Each entry MUST
    /// be commented with the rationale.
    /// </summary>
    private static readonly IReadOnlyCollection<string> ExemptModuleNamespaces =
    [
        // Contacts owns the contact aggregate; the indexed-path handler covers its audit rows.
        "Nexora.Modules.Contacts",
        // Identity emits UserContactLinked/Unlinked which carry only IDs + timestamps; no PII strings.
        "Nexora.Modules.Identity",
    ];

    [Fact]
    public void Modules_PublishingContactIdEvents_DefineAtLeastOneLocator()
    {
        // Renamed from "_RegisterAtLeastOneLocator" → "_DefineAtLeastOneLocator":
        // the test only verifies a TYPE exists in the module assembly. Verifying
        // it is registered in DI would require booting each module's
        // ConfigureServices and asserting the service provider can resolve
        // IContactReferenceLocator — out of scope here since architecture
        // tests run without a host. A separate integration-style test under
        // the host's test project is the right home for the DI-registration
        // check; this test guards against the upstream "we forgot to define
        // a locator type at all" failure mode.
        var moduleAssemblies = LoadModuleAssemblies();

        // Find every IIntegrationEvent type that has a `Guid ContactId`
        // property AND lives inside a module namespace. GetLoadableTypes
        // wraps Assembly.GetTypes() so a partial-load in one type doesn't
        // throw ReflectionTypeLoadException across the entire scan.
        var eventToModule = moduleAssemblies
            .SelectMany(GetLoadableTypes)
            .Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .Where(HasContactIdProperty)
            .Select(t => new { Type = t, ModulePrefix = ExtractModulePrefix(t) })
            .Where(x => x.ModulePrefix is not null)
            .ToList();

        // Build the set of module prefixes that already define a locator.
        var locatorModulePrefixes = moduleAssemblies
            .SelectMany(GetLoadableTypes)
            .Where(t => typeof(IContactReferenceLocator).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .Select(t => ExtractModulePrefix(t))
            .Where(p => p is not null)
            .ToHashSet(StringComparer.Ordinal);

        var offenders = eventToModule
            .Where(e => !ExemptModuleNamespaces.Contains(e.ModulePrefix!))
            .Where(e => !locatorModulePrefixes.Contains(e.ModulePrefix!))
            .Select(e => $"  - {e.Type.FullName} (module {e.ModulePrefix}) has Guid ContactId but no IContactReferenceLocator implementation in the same module assembly.")
            .ToList();

        offenders.Should().BeEmpty(
            "every module emitting a ContactId-bearing integration event must register an IContactReferenceLocator " +
            "(or be added to ExemptModuleNamespaces with a documented rationale).\nOffenders:\n" +
            string.Join("\n", offenders));
    }

    [Fact]
    public void Locators_LiveInModuleAssemblies_NotInSharedKernel_OrAudit()
    {
        var allLocators = LoadModuleAssemblies()
            .Concat([typeof(IContactReferenceLocator).Assembly])
            .SelectMany(GetLoadableTypes)
            .Where(t => typeof(IContactReferenceLocator).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .ToList();

        // Allow test-only stubs in the test assemblies; flag any locator
        // that lives in SharedKernel / Audit (would create a circular
        // dependency or block module-level redaction rules from being
        // discovered via the standard DI registration path).
        var bannedNamespaces = new[] { "Nexora.SharedKernel", "Nexora.Modules.Audit" };
        var offenders = allLocators
            .Where(t => bannedNamespaces.Any(ns =>
                t.Namespace?.StartsWith(ns, StringComparison.Ordinal) == true))
            .ToList();

        offenders.Should().BeEmpty(
            "IContactReferenceLocator implementations must live in their owning module assembly. " +
            $"Offenders: {string.Join(", ", offenders.Select(t => t.FullName))}");
    }

    private static bool HasContactIdProperty(Type t)
    {
        var prop = t.GetProperty("ContactId", BindingFlags.Public | BindingFlags.Instance);
        return prop is not null && prop.PropertyType == typeof(Guid);
    }

    /// <summary>
    /// Extracts the <c>Nexora.Modules.{Name}</c> prefix from a type's
    /// namespace, or null when the type is not in a module assembly.
    /// </summary>
    private static string? ExtractModulePrefix(Type t)
    {
        var ns = t.Namespace ?? string.Empty;
        const string root = "Nexora.Modules.";
        if (!ns.StartsWith(root, StringComparison.Ordinal)) return null;
        var rest = ns[root.Length..];
        var dot = rest.IndexOf('.', StringComparison.Ordinal);
        var moduleName = dot < 0 ? rest : rest[..dot];
        return root + moduleName;
    }

    /// <summary>
    /// Wraps <see cref="Assembly.GetTypes"/> so a single broken type
    /// (missing dependency, version mismatch) does not flake the whole
    /// architecture-test sweep. Returns the loaded types only;
    /// <see cref="ReflectionTypeLoadException.Types"/> may contain nulls
    /// for the failed entries — those are filtered out.
    /// </summary>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    private static IReadOnlyList<Assembly> LoadModuleAssemblies()
    {
        // Force-load every module assembly via a known type per module so
        // reflection actually has them in the AppDomain. Listing the
        // anchors here is cheap and catches missing references.
        var anchors = new[]
        {
            typeof(Nexora.Modules.Contacts.ContactsModule),
            typeof(Nexora.Modules.Identity.IdentityModule),
            typeof(Nexora.Modules.Audit.AuditModule),
            typeof(Nexora.Modules.Documents.DocumentsModule),
            typeof(Nexora.Modules.Notifications.NotificationsModule),
            typeof(Nexora.Modules.Reporting.ReportingModule),
        };
        return anchors.Select(t => t.Assembly).Distinct().ToList();
    }
}
