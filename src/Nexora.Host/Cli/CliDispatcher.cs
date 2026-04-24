namespace Nexora.Host.Cli;

/// <summary>
/// Top-of-main CLI dispatcher (T-006). Called before <c>WebApplication.CreateBuilder</c>
/// so CLI invocations never spin up the web host. Returns <c>true</c> when the dispatcher
/// consumed the args; callers should immediately return <paramref name="exitCode"/> from
/// <c>Main</c>. Returns <c>false</c> to fall through to the normal web-host startup path.
/// </summary>
public static class CliDispatcher
{
    /// <summary>Exit code returned on unknown / malformed CLI invocations.</summary>
    public const int UsageError = 1;

    /// <summary>Exit code returned when a command ran but reported a partial failure.</summary>
    public const int PartialFailure = 2;

    /// <summary>
    /// Inspects <paramref name="args"/>; when the first token is a recognised CLI verb,
    /// runs the matching command synchronously and stores its exit code in
    /// <paramref name="exitCode"/>. Unknown verbs fall through so the web host can start
    /// normally (callers pass the full <c>Main</c> argv).
    /// </summary>
    public static bool TryDispatch(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0 || string.IsNullOrEmpty(args[0])) return false;

        // Verb matching is case-insensitive — operators typing `Demo:Load` from a
        // shell with autocomplete or a Windows-style title-case habit must not see
        // "unknown command, falling through to web host".
        var verb = args[0].ToLowerInvariant();

        switch (verb)
        {
            case "demo:load":
                exitCode = DemoLoadCommand.Run(args.AsSpan(1));
                return true;

            case "--help":
            case "-h":
            case "help":
                PrintUsage();
                exitCode = 0;
                return true;

            default:
                // Unknown first arg is NOT an error here — the web host may legitimately
                // receive positional args from `dotnet run --`. The web host path ignores
                // unknowns, so we do too.
                return false;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Nexora host CLI");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  demo:load --tenant=<guid> --scenario=<name> [--dry-run]");
        Console.WriteLine("      Runs IDemoDataSeeder for the given tenant+scenario. Requires");
        Console.WriteLine("      the tenant schema to already exist — tenant provisioning is a");
        Console.WriteLine("      separate admin API (see docs/roadmap/phases/phase-1.5-bridge.md).");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  success (or all modules already seeded)");
        Console.WriteLine($"  {UsageError}  usage / validation error");
        Console.WriteLine($"  {PartialFailure}  one or more modules failed during seeding");
    }
}
