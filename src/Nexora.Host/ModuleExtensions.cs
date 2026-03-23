using System.Reflection;
using Nexora.Infrastructure.Jobs;
using Nexora.SharedKernel.Abstractions.Modules;

namespace Nexora.Host;

/// <summary>
/// Extension methods for discovering, registering, and initializing Nexora modules.
/// </summary>
public static class ModuleExtensions
{
    private static readonly List<IModule> _modules = [];

    /// <summary>
    /// Discovers all IModule implementations and registers their services.
    /// </summary>
    public static IServiceCollection AddNexoraModules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Ensure module assemblies are loaded — .NET loads assemblies lazily,
        // so AppDomain.GetAssemblies() may not include module DLLs in Docker/publish.
        var appDir = AppContext.BaseDirectory;
        foreach (var dll in Directory.GetFiles(appDir, "Nexora.Modules.*.dll"))
        {
            try
            {
                Assembly.LoadFrom(dll);
            }
            catch
            {
                // Skip assemblies that can't be loaded
            }
        }

        // Discover all IModule implementations from loaded assemblies
        var moduleTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IModule).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
            .ToList();

        foreach (var moduleType in moduleTypes)
        {
            var module = (IModule)Activator.CreateInstance(moduleType)!;
            _modules.Add(module);
        }

        // Validate dependencies
        var moduleNames = _modules.Select(m => m.Name).ToHashSet();
        foreach (var module in _modules)
        {
            foreach (var dep in module.Dependencies)
            {
                if (!moduleNames.Contains(dep))
                {
                    throw new InvalidOperationException(
                        $"Module '{module.Name}' requires '{dep}' but it is not registered.");
                }
            }
        }

        // Register services for each module
        foreach (var module in _modules)
        {
            module.ConfigureServices(services, configuration);
            module.ConfigureEventHandlers(services);
        }

        // Register modules in DI for injection
        foreach (var module in _modules)
        {
            services.AddSingleton(module);
        }
        services.AddSingleton<IReadOnlyList<IModule>>(_modules);

        return services;
    }

    /// <summary>
    /// Maps API endpoints for all registered modules.
    /// </summary>
    public static WebApplication MapNexoraModuleEndpoints(this WebApplication app)
    {
        foreach (var module in _modules)
        {
            var group = app.MapGroup($"/api/v1/{module.Name}")
                .AddEndpointFilter<TraceIdEndpointFilter>();
            module.MapEndpoints(group);
        }

        return app;
    }

    /// <summary>
    /// Runs startup hooks and configures jobs for all modules.
    /// </summary>
    public static async Task RunModuleStartupAsync(this WebApplication app)
    {
        var scheduler = app.Services.GetRequiredService<IJobScheduler>();

        foreach (var module in _modules)
        {
            module.ConfigureJobs(scheduler);
            await module.OnStartupAsync(CancellationToken.None);
        }
    }
}
