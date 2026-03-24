using System.Text.RegularExpressions;

namespace Nexora.Modules.Reporting.Infrastructure.Services;

/// <summary>
/// Validates SQL queries to ensure they are read-only SELECT or WITH statements.
/// Prevents DDL/DML injection in report definitions.
/// </summary>
public static partial class SqlQueryValidator
{
    private static readonly string[] ForbiddenKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "CREATE", "TRUNCATE",
        "EXEC", "EXECUTE", "GRANT", "REVOKE", "MERGE", "CALL", "COPY"
    ];

    /// <summary>Returns true if the query is a safe read-only SQL statement.</summary>
    public static bool IsValid(string queryText, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(queryText))
        {
            errorMessage = "Query text is empty";
            return false;
        }

        var trimmed = queryText.Trim();

        // Must start with SELECT or WITH
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Query must start with SELECT or WITH";
            return false;
        }

        // Check for forbidden DML/DDL keywords as standalone words
        foreach (var keyword in ForbiddenKeywords)
        {
            var pattern = $@"\b{keyword}\b";
            if (Regex.IsMatch(trimmed, pattern, RegexOptions.IgnoreCase))
            {
                errorMessage = $"Forbidden keyword: {keyword}";
                return false;
            }
        }

        // Semicolons outside string literals (prevent statement chaining)
        // Simple check: no semicolons at all (parameterized queries don't need them)
        if (trimmed.Contains(';'))
        {
            errorMessage = "Semicolons are not allowed";
            return false;
        }

        return true;
    }
}
