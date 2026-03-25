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
        "EXEC", "EXECUTE", "GRANT", "REVOKE", "MERGE", "CALL", "COPY",
        "SET", "DO", "COMMENT", "VACUUM", "REINDEX", "CLUSTER", "LOAD"
    ];

    private static readonly string[] ForbiddenFunctions =
    [
        "dblink", "lo_import", "lo_export", "pg_read_file",
        "pg_write_file", "pg_execute_server_program"
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

        // Semicolons outside string literals (prevent statement chaining)
        // Simple check: no semicolons at all (parameterized queries don't need them)
        if (trimmed.Contains(';'))
        {
            errorMessage = "Semicolons are not allowed";
            return false;
        }

        // Strip SQL comments before keyword checking to prevent bypass via comments
        var stripped = StripSqlComments(trimmed);

        // Must start with SELECT or WITH
        var strippedTrimmed = stripped.TrimStart();
        if (!strippedTrimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            !strippedTrimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Query must start with SELECT or WITH";
            return false;
        }

        // Check for forbidden DML/DDL keywords as standalone words
        foreach (var keyword in ForbiddenKeywords)
        {
            var pattern = $@"\b{keyword}\b";
            if (Regex.IsMatch(stripped, pattern, RegexOptions.IgnoreCase))
            {
                errorMessage = $"Forbidden keyword: {keyword}";
                return false;
            }
        }

        // Check for forbidden functions
        foreach (var function in ForbiddenFunctions)
        {
            var pattern = $@"\b{Regex.Escape(function)}\s*\(";
            if (Regex.IsMatch(stripped, pattern, RegexOptions.IgnoreCase))
            {
                errorMessage = $"Forbidden function: {function}";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Strips single-line (-- ...) and block (/* ... */) SQL comments from the query text.
    /// </summary>
    private static string StripSqlComments(string sql)
    {
        // Remove block comments (non-greedy, handles nested is not needed for safety)
        var result = Regex.Replace(sql, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        // Remove single-line comments
        result = Regex.Replace(result, @"--[^\r\n]*", " ");
        return result;
    }
}
