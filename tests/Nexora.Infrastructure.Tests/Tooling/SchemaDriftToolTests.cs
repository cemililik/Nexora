using System.Diagnostics;
using Xunit;

namespace Nexora.Infrastructure.Tests.Tooling;

/// <summary>
/// T-024: opt-in wrapper that runs <c>tools/check-schema-drift.py</c> from the
/// test suite. Skipped by default — requires docker compose + Postgres +
/// python3, none of which are guaranteed in every CI runner. Opt-in via
/// <c>NEXORA_SCHEMA_DRIFT_ENABLED=1</c> so nightly CI and local devs can run
/// the detector while PR CI stays fast.
///
/// <para>
/// When enabled, the test fails with the script's full stdout/stderr when the
/// script exits non-zero — the CI log then tells you which column drifted
/// without needing a second run.
/// </para>
/// </summary>
[Trait("Category", "Tooling")]
public sealed class SchemaDriftToolTests
{
    private const string EnableEnvVar = "NEXORA_SCHEMA_DRIFT_ENABLED";

    [SkippableFact]
    public void CheckSchemaDrift_Script_ExitsZero_AgainstDevTenantSchema()
    {
        var enabled = Environment.GetEnvironmentVariable(EnableEnvVar);
        Skip.If(string.IsNullOrEmpty(enabled) || enabled == "0",
            $"Set {EnableEnvVar}=1 to run the drift detector (requires docker compose running).");

        var repoRoot = FindRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "tools", "check-schema-drift.py");
        Skip.IfNot(File.Exists(scriptPath),
            $"{scriptPath} not found — tools/ layout may have moved.");

        Skip.IfNot(IsOnPath("python3"),
            "python3 is not on PATH.");

        var psi = new ProcessStartInfo
        {
            FileName = "python3",
            ArgumentList = { scriptPath },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = repoRoot,
        };

        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(120_000);

        if (proc.ExitCode != 0)
        {
            var combined =
                $"tools/check-schema-drift.py exited with code {proc.ExitCode}.{Environment.NewLine}" +
                $"--- stdout ---{Environment.NewLine}{stdout}{Environment.NewLine}" +
                $"--- stderr ---{Environment.NewLine}{stderr}";
            Assert.Fail(combined);
        }
    }

    private static bool IsOnPath(string executable)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in paths)
        {
            var candidate = Path.Combine(p, executable);
            if (File.Exists(candidate)) return true;
        }
        return false;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Nexora.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not locate repository root starting from " + AppContext.BaseDirectory);
    }
}
