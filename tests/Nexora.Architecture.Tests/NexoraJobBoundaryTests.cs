using System.Reflection;
using System.Text.RegularExpressions;
using Nexora.Architecture.Tests.Helpers;

namespace Nexora.Architecture.Tests;

/// <summary>
/// T-018 guard: every <c>NexoraJob&lt;TParams&gt;</c> inherits the base <c>RunAsync</c>
/// unchanged, and that method must call <c>SetTenant</c> BEFORE <c>ExecuteAsync</c>.
/// Tenant context is what routes the DbContext to the right schema; any future drift
/// (an override, a <c>new</c> shadow, a reordering) silently breaks multi-tenant
/// isolation — exactly the regression this test exists to catch.
/// </summary>
public sealed class NexoraJobBoundaryTests
{
    private static readonly string RepoSrcRoot = FindRepoSrcRoot();

    private static string FindRepoSrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Nexora.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return Path.Combine(dir.FullName, "src");
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not locate repository root starting from " + AppContext.BaseDirectory);
    }

    [Fact]
    public void NexoraJob_RunAsync_SetsTenantBeforeExecuteAsync()
    {
        var path = Path.Combine(
            RepoSrcRoot, "Nexora.SharedKernel", "Abstractions", "Jobs", "NexoraJob.cs");
        File.Exists(path).Should().BeTrue("NexoraJob.cs must exist");

        var source = File.ReadAllText(path);

        var setTenantIdx = source.IndexOf("SetTenant(", StringComparison.Ordinal);
        var executeIdx = source.IndexOf("ExecuteAsync(parameters", StringComparison.Ordinal);

        setTenantIdx.Should().BeGreaterThan(0,
            "NexoraJob.RunAsync must call ITenantContextAccessor.SetTenant(...) — without it, DbContext cannot route to the tenant schema.");
        executeIdx.Should().BeGreaterThan(setTenantIdx,
            "NexoraJob.RunAsync must call SetTenant BEFORE ExecuteAsync; otherwise the subclass runs queries against an unset or stale tenant context.");
    }

    [Fact]
    public void NexoraJob_RunAsync_IsNotVirtual_SoSubclassesCannotBypassTenantSetup()
    {
        var baseType = typeof(Nexora.SharedKernel.Abstractions.Jobs.NexoraJob<>);
        var runAsync = baseType.GetMethod(
            "RunAsync", BindingFlags.Instance | BindingFlags.Public);

        runAsync.Should().NotBeNull("NexoraJob<TParams>.RunAsync must exist as the public Hangfire entry point");
        runAsync!.IsVirtual.Should().BeFalse(
            "RunAsync must stay non-virtual — if a subclass overrides it, the SetTenant(...) step can be skipped and tenant isolation breaks silently.");
    }

    [Fact]
    public void NexoraJob_Subclasses_DoNotShadowRunAsync()
    {
        // Scan every *.cs under src/Modules/**/Jobs and assert no file declares a new
        // RunAsync method — the base implementation must be the only one.
        var jobFiles = Directory.EnumerateFiles(
            Path.Combine(RepoSrcRoot, "Modules"), "*.cs", SearchOption.AllDirectories)
            .Where(p => p.Contains($"{Path.DirectorySeparatorChar}Jobs{Path.DirectorySeparatorChar}"))
            .ToArray();

        jobFiles.Should().NotBeEmpty("expected at least one job file under Modules/**/Jobs/");

        var offenders = new List<string>();
        var runAsyncDecl = new Regex(
            @"\b(public|protected|internal|private)\s+(?:new\s+|override\s+|async\s+)*[\w<>?,\s]*?\bRunAsync\s*\(",
            RegexOptions.Compiled);

        foreach (var file in jobFiles)
        {
            // Strip comments + string literals before scanning so the regex
            // never false-hits on a multi-line doc comment that mentions
            // RunAsync(...) in prose, or on a sample string literal embedded
            // in a test fixture under Modules/**/Jobs/.
            var content = SourceTextStripper.StripCommentsAndStrings(File.ReadAllText(file));
            if (runAsyncDecl.IsMatch(content))
                offenders.Add(Path.GetRelativePath(RepoSrcRoot, file));
        }

        offenders.Should().BeEmpty(
            "subclasses of NexoraJob<TParams> must not declare their own RunAsync — it shadows the base and skips SetTenant. Implement ExecuteAsync instead.");
    }
}
