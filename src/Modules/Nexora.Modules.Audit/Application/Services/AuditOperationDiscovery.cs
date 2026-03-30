namespace Nexora.Modules.Audit.Application.Services;

/// <summary>
/// Provides utility methods for discovering and classifying auditable operations
/// from command/query type metadata (names, namespaces).
/// </summary>
public static class AuditOperationDiscovery
{
    /// <summary>Extracts the module name from the namespace (e.g., Nexora.Modules.Identity.Application.Commands → identity).</summary>
    public static string ExtractModuleName(string? ns)
    {
        if (string.IsNullOrEmpty(ns))
            return "unknown";

        // Namespace pattern: Nexora.Modules.{ModuleName}.Application.Commands
        var parts = ns.Split('.');
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (string.Equals(parts[i], "Modules", StringComparison.Ordinal) && i + 1 < parts.Length)
            {
                return parts[i + 1].ToLowerInvariant();
            }
        }

        return "unknown";
    }

    /// <summary>Extracts a human-readable operation name by removing the "Command" suffix.</summary>
    public static string ExtractCommandOperationName(string className)
    {
        return className.EndsWith("Command", StringComparison.Ordinal)
            ? className[..^7]
            : className;
    }

    /// <summary>
    /// Extracts query operation name by removing "Query" suffix and adding "Query." prefix.
    /// Example: "GetUsersQuery" → "Query.GetUsers"
    /// </summary>
    public static string ExtractQueryOperationName(string className)
    {
        var baseName = className.EndsWith("Query", StringComparison.Ordinal)
            ? className[..^5]
            : className;

        return $"Query.{baseName}";
    }

    /// <summary>Determines the operation type from the operation name prefix.</summary>
    public static string DetermineOperationType(string operationName)
    {
        if (operationName.StartsWith("Create", StringComparison.Ordinal))
            return "Create";
        if (operationName.StartsWith("Update", StringComparison.Ordinal))
            return "Update";
        if (operationName.StartsWith("Delete", StringComparison.Ordinal))
            return "Delete";
        if (operationName.StartsWith("Remove", StringComparison.Ordinal))
            return "Delete";
        return "Action";
    }
}
