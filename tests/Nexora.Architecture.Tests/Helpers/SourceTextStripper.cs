namespace Nexora.Architecture.Tests.Helpers;

/// <summary>
/// Roslyn-lite source-text stripper shared by every architecture test that
/// runs <see cref="System.Text.RegularExpressions.Regex"/> scans against C#
/// source. Removes <c>// line</c> and <c>/* block */</c> comments plus string
/// literals (<c>"…"</c>, <c>@"…"</c>) so a regex match can never false-hit
/// on commented-out examples or string content.
///
/// <para>
/// Not a real parser — adequate for grep-style guards against well-known
/// call shapes (e.g. <c>RunAsync(</c>, <c>Permission.Create(</c>) and a
/// handful of module-name string constants. The strip-strings option exists
/// because some scans (e.g. cross-module hardcoded module names) MUST see
/// string content while others (e.g. method-decl regex) MUST NOT.
/// </para>
/// <para>
/// <b>Known limitations</b> — this stripper handles only the common string
/// forms used in this codebase: regular <c>"…"</c> and verbatim <c>@"…"</c>.
/// It DOES NOT fully understand:
/// <list type="bullet">
///   <item>Raw string literals (<c>"""…"""</c> and <c>"""" … """"</c>) — the
///         opening/closing quote count is not tracked; inner content may be
///         treated as code.</item>
///   <item>Interpolated forms (<c>$"…"</c>, <c>$@"…"</c>, <c>@$"…"</c>,
///         <c>$"""…"""</c>) — the <c>$</c> prefix is not recognised, so the
///         interpolation braces and expressions may leak into the "stripped"
///         output.</item>
/// </list>
/// If a future architecture test must guard code that uses these forms, prefer
/// a Roslyn-based syntax walk (<c>Microsoft.CodeAnalysis.CSharp.SyntaxTree</c>)
/// over extending this helper.
/// </para>
/// </summary>
internal static class SourceTextStripper
{
    /// <summary>
    /// Removes <c>// line</c> and <c>/* block */</c> comments only;
    /// preserves string literals. Used when the scan target is a string
    /// constant (e.g. forbidden module-name tokens).
    /// </summary>
    public static string StripComments(string source) => Strip(source, stripStrings: false);

    /// <summary>
    /// Removes both comments AND string literals. Used when the scan target
    /// is executable code only (e.g. method declarations, type references).
    /// </summary>
    public static string StripCommentsAndStrings(string source) => Strip(source, stripStrings: true);

    private static string Strip(string source, bool stripStrings)
    {
        var sb = new System.Text.StringBuilder(source.Length);
        int i = 0;
        while (i < source.Length)
        {
            char c = source[i];
            char next = i + 1 < source.Length ? source[i + 1] : '\0';

            if (c == '/' && next == '/')
            {
                while (i < source.Length && source[i] != '\n') i++;
                continue;
            }
            if (c == '/' && next == '*')
            {
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/')) i++;
                i = System.Math.Min(i + 2, source.Length);
                continue;
            }

            if (stripStrings)
            {
                // Verbatim string @"..." — "" is an escaped quote.
                if (c == '@' && next == '"')
                {
                    i += 2;
                    while (i < source.Length)
                    {
                        if (source[i] == '"' && i + 1 < source.Length && source[i + 1] == '"') { i += 2; continue; }
                        if (source[i] == '"') { i++; break; }
                        i++;
                    }
                    continue;
                }
                // Regular string "..." — \" is an escape.
                if (c == '"')
                {
                    i++;
                    while (i < source.Length)
                    {
                        if (source[i] == '\\' && i + 1 < source.Length) { i += 2; continue; }
                        if (source[i] == '"') { i++; break; }
                        if (source[i] == '\n') break;
                        i++;
                    }
                    continue;
                }
            }

            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }
}
