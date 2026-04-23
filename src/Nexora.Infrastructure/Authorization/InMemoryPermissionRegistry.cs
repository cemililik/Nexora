using System.Collections.Concurrent;
using Nexora.SharedKernel.Authorization;

namespace Nexora.Infrastructure.Authorization;

/// <summary>
/// In-memory <see cref="IPermissionRegistry"/>. Populated once at host startup by each
/// module's <c>OnStartupAsync</c>; read by <c>IdentityModuleMigration.SeedAsync</c> to
/// flow definitions into per-tenant <c>identity_permissions</c> rows.
/// </summary>
/// <remarks>
/// Thread-safe registration to accommodate parallel module start-ups, but the registry
/// is effectively frozen after the host's initialization phase; no runtime mutation.
/// </remarks>
public sealed class InMemoryPermissionRegistry : IPermissionRegistry
{
    private readonly ConcurrentDictionary<string, PermissionDefinition> _byName = new(StringComparer.Ordinal);
    private readonly List<PermissionDefinition> _inOrder = new();
    private readonly object _orderLock = new();

    /// <inheritdoc />
    public PermissionDefinition Register(
        string module,
        string resource,
        string action,
        string descriptionKey,
        PermissionScope scope = PermissionScope.Tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptionKey);

        var def = new PermissionDefinition(module, resource, action, descriptionKey, scope);
        if (!_byName.TryAdd(def.Name, def))
        {
            var existing = _byName[def.Name];
            throw new InvalidOperationException(
                $"Permission '{def.Name}' is already registered with scope {existing.Scope}. " +
                "Each permission must be declared by exactly one module — see permissions.md §3.");
        }

        lock (_orderLock)
        {
            _inOrder.Add(def);
        }

        return def;
    }

    /// <inheritdoc />
    public bool IsRegistered(string name) => _byName.ContainsKey(name);

    /// <inheritdoc />
    public bool TryGetByName(string name, out PermissionDefinition? definition)
    {
        if (_byName.TryGetValue(name, out var found))
        {
            definition = found;
            return true;
        }
        definition = null;
        return false;
    }

    /// <inheritdoc />
    public IReadOnlyList<PermissionDefinition> GetAll()
    {
        lock (_orderLock)
        {
            return _inOrder.ToArray();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<PermissionDefinition> GetByModule(string module)
    {
        lock (_orderLock)
        {
            return _inOrder.Where(d => d.Module.Equals(module, StringComparison.Ordinal)).ToArray();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<PermissionDefinition> GetByScope(PermissionScope scope)
    {
        lock (_orderLock)
        {
            return _inOrder.Where(d => d.Scope == scope).ToArray();
        }
    }
}
