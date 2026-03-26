using System.Text.RegularExpressions;
using Nexora.Modules.Reporting.Application.Services;

namespace Nexora.Modules.Reporting.Infrastructure.Services;

/// <summary>
/// Validates SQL queries to ensure they are read-only SELECT or WITH statements.
/// Prevents DDL/DML injection in report definitions.
/// </summary>
public sealed partial class SqlQueryValidator : ISqlQueryValidator
{
    /// <summary>Returns true if the query is a safe read-only SQL statement.</summary>
    public bool IsValid(string queryText, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(queryText))
        {
            errorMessage = "lockey_reporting_validation_query_empty";
            return false;
        }

        var trimmed = queryText.Trim();

        // No semicolons allowed anywhere (prevents statement chaining).
        // This also rejects semicolons inside string literals, which is an acceptable trade-off.
        if (trimmed.Contains(';'))
        {
            errorMessage = "lockey_reporting_validation_query_no_semicolons";
            return false;
        }

        // Strip SQL comments before keyword checking to prevent bypass via comments
        var stripped = StripSqlComments(trimmed);

        // Must start with SELECT or WITH
        var strippedTrimmed = stripped.TrimStart();
        if (!strippedTrimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            !strippedTrimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "lockey_reporting_validation_query_must_start_select";
            return false;
        }

        // Check for forbidden DML/DDL keywords as standalone words
        var keywordMatch = ForbiddenKeywordsRegex().Match(stripped);
        if (keywordMatch.Success)
        {
            errorMessage = "lockey_reporting_validation_query_forbidden_keyword";
            return false;
        }

        // Check for forbidden functions
        var functionMatch = ForbiddenFunctionsRegex().Match(stripped);
        if (functionMatch.Success)
        {
            errorMessage = "lockey_reporting_validation_query_forbidden_function";
            return false;
        }

        // Check for SELECT INTO (data export/table creation)
        if (SelectIntoRegex().IsMatch(stripped))
        {
            errorMessage = "lockey_reporting_validation_query_forbidden_keyword";
            return false;
        }

        // Check for row-locking clauses (FOR UPDATE, FOR SHARE, etc.)
        if (ForLockingRegex().IsMatch(stripped))
        {
            errorMessage = "lockey_reporting_validation_query_locking_forbidden";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Strips single-line (-- ...) and block (/* ... */) SQL comments from the query text.
    /// </summary>
    private string StripSqlComments(string sql)
    {
        // Remove block comments (non-greedy, handles nested is not needed for safety)
        var result = BlockCommentRegex().Replace(sql, " ");
        // Remove single-line comments
        result = SingleLineCommentRegex().Replace(result, " ");
        return result;
    }

    [GeneratedRegex(@"\b(INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|TRUNCATE|EXEC|EXECUTE|GRANT|REVOKE|MERGE|CALL|COPY|SET|DO|COMMENT|VACUUM|REINDEX|CLUSTER|LOAD)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ForbiddenKeywordsRegex();

    [GeneratedRegex(@"\b(dblink|lo_import|lo_export|pg_read_file|pg_write_file|pg_execute_server_program)\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex ForbiddenFunctionsRegex();

    [GeneratedRegex(@"\bSELECT\s+.*?\bINTO\b", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SelectIntoRegex();

    [GeneratedRegex(@"\bFOR\s+(UPDATE|NO\s+KEY\s+UPDATE|SHARE|KEY\s+SHARE)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ForLockingRegex();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockCommentRegex();

    [GeneratedRegex(@"--[^\r\n]*")]
    private static partial Regex SingleLineCommentRegex();
}
