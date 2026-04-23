using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexora.Infrastructure;

namespace Nexora.Host.Cli;

/// <summary>
/// Builds the minimal DI container needed by <see cref="DemoLoadCommand"/>.
/// Reuses the same Infrastructure + module registrations as the web host so the
/// CLI and the running API execute demo-seed against identical service graphs —
/// divergence between them would be exactly the kind of bug T-005's architecture
/// test is meant to prevent.
/// </summary>
internal static class DemoLoadHostFactory
{
    /// <summary>
    /// Build a host with infrastructure + modules registered. The host is NOT
    /// started (no web server, no Hangfire); only the DI graph is materialised.
    /// The caller owns the returned <see cref="IHost"/> and MUST dispose it —
    /// <see cref="DemoLoadCommand"/> does so in its <c>await using</c> block.
    /// </summary>
    public static IHost Build()
    {
        // Use the same configuration sources the web host reads so connection
        // strings resolve the same way.
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();

        builder.Services.AddNexoraInfrastructure(builder.Configuration);
        builder.Services.AddNexoraModules(builder.Configuration);

        return builder.Build();
    }
}
