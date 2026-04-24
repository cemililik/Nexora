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
    public void Script_WhenExecutedAgainstDevTenantSchema_ExitsZero()
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

        // Consume stdout and stderr ASYNCHRONOUSLY via DataReceived events.
        // The previous synchronous ReadToEnd() pair could deadlock if the
        // child filled one pipe's buffer while we waited on the other; the
        // event-based pattern reads both pipes concurrently so neither side
        // ever blocks on a full buffer.
        var stdoutSb = new System.Text.StringBuilder();
        var stderrSb = new System.Text.StringBuilder();
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) lock (stdoutSb) stdoutSb.AppendLine(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) lock (stderrSb) stderrSb.AppendLine(e.Data);
        };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        // Observe WaitForExit's bool return — false means the timeout
        // elapsed without exit, in which case we kill the child rather than
        // touching ExitCode (which throws on non-exited processes).
        var exited = proc.WaitForExit(120_000);
        if (!exited)
        {
            // Process.Kill throws InvalidOperationException when the child has
            // already exited in the small race window between WaitForExit
            // returning false and our Kill call. Narrow the catch to that one
            // exception so anything else (access denied, OOM, etc.) surfaces
            // as a real test failure rather than being swallowed silently.
            try { proc.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* child already exited */ }
            // Allow the kill to surface in the streams.
            proc.WaitForExit(5_000);
            Assert.Fail(
                $"tools/check-schema-drift.py did not exit within 120s; killed.{Environment.NewLine}" +
                $"--- stdout (partial) ---{Environment.NewLine}{stdoutSb}{Environment.NewLine}" +
                $"--- stderr (partial) ---{Environment.NewLine}{stderrSb}");
        }

        // The timed WaitForExit can return true before the async output /
        // error event handlers have flushed their last line. Calling the
        // parameterless WaitForExit() explicitly drains the async event pump
        // so the stdout/stderr StringBuilders are guaranteed complete before
        // we read ExitCode and compose any failure message.
        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            var combined =
                $"tools/check-schema-drift.py exited with code {proc.ExitCode}.{Environment.NewLine}" +
                $"--- stdout ---{Environment.NewLine}{stdoutSb}{Environment.NewLine}" +
                $"--- stderr ---{Environment.NewLine}{stderrSb}";
            Assert.Fail(combined);
        }
    }

    private static bool IsOnPath(string executable)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        // On Windows, resolving an executable name requires appending every
        // extension the shell would try (PATHEXT). Without this, `python3` on
        // Windows — which is typically `python3.exe` or a `py.exe` launcher —
        // is never found even when it IS on PATH, and the [SkippableFact]
        // unconditionally skips on Windows CI runners.
        var candidateNames = new List<string> { executable };
        if (OperatingSystem.IsWindows())
        {
            var pathExt = Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.BAT;.CMD;.COM";
            foreach (var ext in pathExt.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                candidateNames.Add(executable + ext);
            }
        }

        foreach (var p in paths)
        {
            foreach (var name in candidateNames)
            {
                var candidate = Path.Combine(p, name);
                if (File.Exists(candidate)) return true;
            }
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
