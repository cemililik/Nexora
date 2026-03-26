namespace Nexora.Modules.Reporting.Application.Services;

/// <summary>
/// Validates SQL queries to ensure they are safe read-only statements.
/// </summary>
public interface ISqlQueryValidator
{
    /// <summary>Returns true if the query is a safe read-only SQL statement.</summary>
    bool IsValid(string queryText, out string? errorMessage);
}
