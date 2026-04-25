namespace Nexora.SharedKernel.Abstractions.Modules;

/// <summary>
/// Centralised helpers over the <see cref="IModule.Dependencies"/> graph.
/// Factored out so install/uninstall/seed/migration code paths share one
/// topological implementation instead of forking the algorithm.
/// </summary>
/// <remarks>
/// <para>
/// T-026 surfaces three distinct read-paths over the same graph:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="OrderByDependencies"/> — install / migrate / seed
///   ordering (deepest dependency first, root last).</description></item>
///   <item><description><see cref="FindInstalledDependents"/> — uninstall blocking:
///   "who would break if I remove this module?".</description></item>
///   <item><description><see cref="ReverseUninstallOrder"/> — cascade uninstall ordering
///   (root last, dependents first) per ADR-0031.</description></item>
/// </list>
/// <para>
/// Comparisons are case-insensitive against <see cref="IModule.Name"/> because
/// module names round-trip through SQL identifiers and JSON payloads where
/// casing is not guaranteed.
/// </para>
/// </remarks>
public static class ModuleDependencyGraph
{
    /// <summary>
    /// Topological sort: a module appears AFTER every module it depends on.
    /// Modules with no dependency on each other are emitted in input order so
    /// results are deterministic and review-friendly.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A cycle exists, or a declared dependency references a name not in
    /// <paramref name="input"/>. Both conditions indicate a module-registration
    /// bug; failing loud is preferred over silently dropping the offender.
    /// </exception>
    public static List<IModule> OrderByDependencies(IReadOnlyList<IModule> input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var byName = input.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        var visited = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var result = new List<IModule>();

        void Visit(IModule module, Stack<string> path)
        {
            if (visited.TryGetValue(module.Name, out var done))
            {
                if (done) return;
                throw new InvalidOperationException(
                    $"Cycle detected in IModule dependency graph: {string.Join(" -> ", path.Reverse())} -> {module.Name}");
            }
            visited[module.Name] = false;
            path.Push(module.Name);

            foreach (var depName in module.Dependencies)
            {
                if (!byName.TryGetValue(depName, out var dep))
                {
                    throw new InvalidOperationException(
                        $"Module '{module.Name}' depends on '{depName}' but no such module is registered.");
                }
                Visit(dep, path);
            }

            path.Pop();
            visited[module.Name] = true;
            result.Add(module);
        }

        foreach (var module in input)
        {
            if (!visited.ContainsKey(module.Name))
                Visit(module, new Stack<string>());
        }
        return result;
    }

    /// <summary>
    /// Returns every module in <paramref name="installedModules"/> whose
    /// <see cref="IModule.Dependencies"/> contains <paramref name="moduleName"/>
    /// — directly or transitively. Used by the uninstall command to reject a
    /// non-cascade uninstall that would leave a dependent module broken.
    /// </summary>
    /// <remarks>
    /// Walks the graph transitively: if A depends on B and B depends on C,
    /// then C's dependent set includes both A and B. The result is in the
    /// SAME ORDER as <see cref="OrderByDependencies"/> over the installed
    /// set (leaf-first / root-last); <see cref="ReverseUninstallOrder"/>
    /// reverses it for cascade uninstall — callers wanting cascade order
    /// should call <see cref="ReverseUninstallOrder"/> instead of reversing
    /// this list themselves (review #30 follow-up — earlier doc said
    /// "deepest-first" which was misleading).
    /// </remarks>
    public static IReadOnlyList<IModule> FindInstalledDependents(
        string moduleName,
        IReadOnlyList<IModule> installedModules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(installedModules);

        var byName = installedModules.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        var dependents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Cycle-safe DependsOn — a malformed input graph (A → B → A)
        // would otherwise stack-overflow before OrderByDependencies'
        // cycle detection ran (review #30). Track in-flight names per
        // call so the recursion bails on revisit instead of recursing
        // forever.
        bool DependsOn(IModule candidate, HashSet<string> visiting)
        {
            if (!visiting.Add(candidate.Name)) return false;
            try
            {
                foreach (var depName in candidate.Dependencies)
                {
                    if (string.Equals(depName, moduleName, StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (dependents.Contains(depName))
                        return true;
                    if (byName.TryGetValue(depName, out var transitive) && DependsOn(transitive, visiting))
                        return true;
                }
                return false;
            }
            finally
            {
                visiting.Remove(candidate.Name);
            }
        }

        // Iterate to a fixed point so transitive dependents land in the set
        // regardless of input order.
        bool changed;
        do
        {
            changed = false;
            foreach (var module in installedModules)
            {
                if (string.Equals(module.Name, moduleName, StringComparison.OrdinalIgnoreCase)) continue;
                if (dependents.Contains(module.Name)) continue;
                if (DependsOn(module, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                {
                    dependents.Add(module.Name);
                    changed = true;
                }
            }
        }
        while (changed);

        // Order against the FULL installed set then filter — sorting the
        // dependent subset alone would fail when a dependent's transitive
        // dependency lives outside the subset (e.g. dependent depends on
        // the target we're removing).
        var orderedFull = OrderByDependencies(installedModules);
        return orderedFull.Where(m => dependents.Contains(m.Name)).ToList();
    }

    /// <summary>
    /// Builds the cascade-uninstall sequence for <paramref name="targetModuleName"/>:
    /// every installed dependent followed by the target itself, in reverse-
    /// dependency order (a module appears BEFORE every module it depends on).
    /// This is the order the cascade orchestrator MUST iterate per ADR-0031.
    /// </summary>
    public static IReadOnlyList<IModule> ReverseUninstallOrder(
        string targetModuleName,
        IReadOnlyList<IModule> installedModules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetModuleName);
        ArgumentNullException.ThrowIfNull(installedModules);

        var target = installedModules.FirstOrDefault(m =>
            string.Equals(m.Name, targetModuleName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Target module '{targetModuleName}' is not in the installed-module set.");

        var dependents = FindInstalledDependents(targetModuleName, installedModules);
        // Caller wants reverse-dependency order: dependents first, deepest
        // dependent (the leaf of the install graph) at the head, target last.
        var ordered = new List<IModule>(dependents.Count + 1);
        ordered.AddRange(dependents.Reverse());
        ordered.Add(target);
        return ordered;
    }
}
