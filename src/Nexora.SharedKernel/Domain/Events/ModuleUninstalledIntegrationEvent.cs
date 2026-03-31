namespace Nexora.SharedKernel.Domain.Events;

/// <summary>Published when a module is uninstalled for a tenant.</summary>
public sealed record ModuleUninstalledIntegrationEvent : IntegrationEventBase
{
    /// <summary>Gets the name of the uninstalled module.</summary>
    public required string ModuleName { get; init; }

    /// <summary>Gets the tenant identifier as a Guid.</summary>
    public required Guid TenantIdGuid { get; init; }
}
