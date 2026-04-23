using System.Text.RegularExpressions;

namespace Nexora.Architecture.Tests;

/// <summary>
/// Enforces T-020 / ADR-004 / permissions.md §3 boundary: permission declarations
/// (<c>Permission.Create(...)</c>) live in exactly one place — each module's
/// <c>OnStartupAsync</c> calls <c>IPermissionRegistry.Register(...)</c>; the DB-writing
/// side lives in <c>IdentityModuleMigration</c> and constructs <see cref="object"/>
/// instances from registry data, not hand-rolled constants.
/// </summary>
public sealed class PermissionRegistryBoundaryTests
{
    private static readonly string RepoSrcRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    [Fact]
    public void DevelopmentSeed_MustNotContainHardcodedPermissionCreate()
    {
        var path = Path.Combine(RepoSrcRoot, "Nexora.Host", "DevelopmentSeed.cs");
        File.Exists(path).Should().BeTrue("DevelopmentSeed.cs must exist");

        var content = File.ReadAllText(path);
        var hits = Regex.Matches(content, @"Permission\.Create\s*\(");

        hits.Count.Should().Be(0,
            "DevelopmentSeed.cs must not hardcode permission declarations — they belong in each " +
            "module's IModule.OnStartupAsync via IPermissionRegistry.Register(...). See ADR-004.");
    }

    [Fact]
    public void IdentityModuleMigration_MustNotContainCrossModulePermissionCreate()
    {
        var path = Path.Combine(
            RepoSrcRoot, "Modules", "Nexora.Modules.Identity",
            "Infrastructure", "IdentityModuleMigration.cs");
        File.Exists(path).Should().BeTrue("IdentityModuleMigration.cs must exist");

        var content = File.ReadAllText(path);

        // Hardcoded module strings besides "identity"/"platform" (Identity owns both) are
        // evidence of the pre-T-020 central seed list having been smuggled back in.
        var forbiddenModules = new[] { "\"contacts\"", "\"documents\"", "\"notifications\"",
            "\"reporting\"", "\"audit\"", "\"crm\"", "\"finance\"", "\"subscription\"" };

        var offenders = forbiddenModules
            .Where(m => content.Contains(m, StringComparison.Ordinal))
            .ToArray();

        offenders.Should().BeEmpty(
            "IdentityModuleMigration must read cross-module permissions from the registry, " +
            "not hardcode them. Offending module tokens: " + string.Join(", ", offenders));
    }

    [Fact]
    public void DevelopmentSeed_MustNotContainCreateDefaultPermissions()
    {
        var path = Path.Combine(RepoSrcRoot, "Nexora.Host", "DevelopmentSeed.cs");
        var content = File.ReadAllText(path);
        content.Should().NotMatchRegex(
            @"private\s+static\s+Permission\[\]\s+CreateDefaultPermissions",
            because: "DevelopmentSeed.CreateDefaultPermissions() was removed by T-020; " +
                     "permissions flow from the registry via IdentityModuleMigration.");
    }
}
