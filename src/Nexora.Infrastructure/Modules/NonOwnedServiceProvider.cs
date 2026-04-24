using Nexora.SharedKernel.Abstractions.Modules;

namespace Nexora.Infrastructure.Modules;

/// <summary>
/// Default <see cref="INonOwnedServiceProvider"/> — delegates resolution to a
/// scoped <see cref="IServiceProvider"/> the orchestrator owns. Does not
/// implement <see cref="IDisposable"/> at all, so a module that mistakenly
/// writes <c>using var sp = context.ScopedServices;</c> fails at compile time
/// instead of silently tearing down the orchestrator's scope.
/// </summary>
internal sealed class NonOwnedServiceProvider(IServiceProvider inner) : INonOwnedServiceProvider
{
    public object? GetService(Type serviceType) => inner.GetService(serviceType);
}
