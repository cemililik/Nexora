using Nexora.Modules.Contacts.Domain.Entities;

namespace Nexora.Modules.Contacts.Domain.Services;

/// <summary>
/// Calculates duplicate score between two contacts.
/// Score range: 0 (no match) to 100 (exact duplicate).
/// </summary>
public sealed class DuplicateDetectionService
{
    /// <summary>Default threshold above which contacts are considered potential duplicates.</summary>
    public const int DefaultThreshold = 40;

    /// <summary>Calculates a duplicate confidence score between two contacts.</summary>
    public int CalculateScore(Contact source, Contact candidate)
    {
        var score = 0;

        // Exact email match: +40 points
        if (!string.IsNullOrWhiteSpace(source.Email) &&
            !string.IsNullOrWhiteSpace(candidate.Email) &&
            string.Equals(source.Email, candidate.Email, StringComparison.OrdinalIgnoreCase))
        {
            score += 40;
        }

        // Exact phone match: +30 points
        if (!string.IsNullOrWhiteSpace(source.Phone) &&
            !string.IsNullOrWhiteSpace(candidate.Phone) &&
            NormalizePhone(source.Phone) == NormalizePhone(candidate.Phone))
        {
            score += 30;
        }

        // Name similarity: up to +25 points
        score += CalculateNameScore(source, candidate);

        // Company name match: +5 points
        if (!string.IsNullOrWhiteSpace(source.CompanyName) &&
            !string.IsNullOrWhiteSpace(candidate.CompanyName) &&
            string.Equals(source.CompanyName, candidate.CompanyName, StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        return Math.Min(score, 100);
    }

    private int CalculateNameScore(Contact source, Contact candidate)
    {
        var score = 0;

        // Exact first name match: +10
        if (!string.IsNullOrWhiteSpace(source.FirstName) &&
            !string.IsNullOrWhiteSpace(candidate.FirstName))
        {
            if (string.Equals(source.FirstName, candidate.FirstName, StringComparison.OrdinalIgnoreCase))
                score += 10;
            else if (LevenshteinDistance(source.FirstName.ToLower(), candidate.FirstName.ToLower()) <= 2)
                score += 5;
        }

        // Exact last name match: +15
        if (!string.IsNullOrWhiteSpace(source.LastName) &&
            !string.IsNullOrWhiteSpace(candidate.LastName))
        {
            if (string.Equals(source.LastName, candidate.LastName, StringComparison.OrdinalIgnoreCase))
                score += 15;
            else if (LevenshteinDistance(source.LastName.ToLower(), candidate.LastName.ToLower()) <= 2)
                score += 8;
        }

        return score;
    }

    private int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        var m = s.Length;
        var n = t.Length;
        var d = new int[m + 1, n + 1];

        for (var i = 0; i <= m; i++) d[i, 0] = i;
        for (var j = 0; j <= n; j++) d[0, j] = j;

        for (var i = 1; i <= m; i++)
        {
            for (var j = 1; j <= n; j++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[m, n];
    }

    private string NormalizePhone(string phone)
    {
        return new string(phone.Where(char.IsDigit).ToArray());
    }
}
