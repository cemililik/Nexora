namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>
/// Preview payload returned by <c>POST /contacts/import/preview</c>. Contains the
/// detected source headers plus up to 5 sample rows (header-keyed) so that the admin
/// UI can present a column-mapping table.
/// </summary>
/// <param name="Headers">Detected source column headers in declaration order.</param>
/// <param name="Rows">First N rows keyed by detected header. Case-insensitive lookup.</param>
/// <param name="TotalRowCount">Number of data rows detected in the file (excluding the header row).</param>
public sealed record ContactImportPreviewDto(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows,
    int TotalRowCount);
