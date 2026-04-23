using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;

namespace Nexora.Modules.Contacts.Infrastructure.Parsers;

/// <summary>
/// Header-preserving parser for contact import files. Produces a list of rows
/// keyed by the detected source header name, enabling the import pipeline to
/// apply a user-supplied column mapping at a later stage.
/// </summary>
public static class ContactImportParser
{
    private const string CsvFormat = "csv";
    private const string XlsxFormat = "xlsx";

    /// <summary>Detects and returns the header row of the uploaded file.</summary>
    /// <param name="content">Raw file bytes.</param>
    /// <param name="format">Either <c>csv</c> or <c>xlsx</c> (case-insensitive).</param>
    public static IReadOnlyList<string> ParseHeaders(byte[] content, string format)
    {
        return format.ToLowerInvariant() switch
        {
            CsvFormat => ParseCsvHeaders(content),
            XlsxFormat => ParseXlsxHeaders(content),
            _ => throw new NotSupportedException($"Unsupported import format: {format}")
        };
    }

    /// <summary>
    /// Parses the file into header-keyed rows. <paramref name="skip"/> / <paramref name="take"/>
    /// allow partial reads (e.g. preview of first 5 rows).
    /// </summary>
    public static List<IReadOnlyDictionary<string, string?>> ParseRows(
        byte[] content,
        string format,
        int? skip = 0,
        int? take = null)
    {
        return format.ToLowerInvariant() switch
        {
            CsvFormat => ParseCsvRows(content, skip ?? 0, take),
            XlsxFormat => ParseXlsxRows(content, skip ?? 0, take),
            _ => throw new NotSupportedException($"Unsupported import format: {format}")
        };
    }

    private static List<string> ParseCsvHeaders(byte[] content)
    {
        using var reader = new StreamReader(new MemoryStream(content));
        using var csv = new CsvReader(reader, BuildCsvConfig());

        if (!csv.Read())
            return [];
        csv.ReadHeader();

        return csv.HeaderRecord?
            .Select(h => h?.Trim() ?? string.Empty)
            .Where(h => !string.IsNullOrEmpty(h))
            .ToList() ?? [];
    }

    private static List<IReadOnlyDictionary<string, string?>> ParseCsvRows(
        byte[] content,
        int skip,
        int? take)
    {
        using var reader = new StreamReader(new MemoryStream(content));
        using var csv = new CsvReader(reader, BuildCsvConfig());

        if (!csv.Read())
            return [];
        csv.ReadHeader();

        var headers = csv.HeaderRecord?
            .Select(h => h?.Trim() ?? string.Empty)
            .ToArray() ?? [];

        var result = new List<IReadOnlyDictionary<string, string?>>();
        var skipped = 0;
        while (csv.Read())
        {
            if (skipped < skip)
            {
                skipped++;
                continue;
            }

            if (take is { } limit && result.Count >= limit)
                break;

            var dict = new Dictionary<string, string?>(headers.Length, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Length; i++)
            {
                var header = headers[i];
                if (string.IsNullOrEmpty(header))
                    continue;

                var value = csv.GetField(i)?.Trim();
                dict[header] = string.IsNullOrEmpty(value) ? null : value;
            }
            result.Add(dict);
        }

        return result;
    }

    private static CsvConfiguration BuildCsvConfig() => new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        HeaderValidated = null,
        MissingFieldFound = null,
    };

    private static List<string> ParseXlsxHeaders(byte[] content)
    {
        using var workbook = new XLWorkbook(new MemoryStream(content));
        var worksheet = workbook.Worksheet(1);
        var headerRow = worksheet.Row(1);
        var lastHeaderCell = headerRow.LastCellUsed();
        if (lastHeaderCell is null)
            return [];

        var headers = new List<string>();
        for (var col = 1; col <= lastHeaderCell.Address.ColumnNumber; col++)
        {
            var value = headerRow.Cell(col).GetString().Trim();
            if (!string.IsNullOrEmpty(value))
                headers.Add(value);
        }
        return headers;
    }

    private static List<IReadOnlyDictionary<string, string?>> ParseXlsxRows(
        byte[] content,
        int skip,
        int? take)
    {
        using var workbook = new XLWorkbook(new MemoryStream(content));
        var worksheet = workbook.Worksheet(1);
        var headerRow = worksheet.Row(1);
        var lastHeaderCell = headerRow.LastCellUsed();
        if (lastHeaderCell is null)
            return [];

        var headers = new List<(string Name, int Column)>();
        for (var col = 1; col <= lastHeaderCell.Address.ColumnNumber; col++)
        {
            var value = headerRow.Cell(col).GetString().Trim();
            if (!string.IsNullOrEmpty(value))
                headers.Add((value, col));
        }

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var result = new List<IReadOnlyDictionary<string, string?>>();
        var skipped = 0;

        for (var rowNum = 2; rowNum <= lastRow; rowNum++)
        {
            var row = worksheet.Row(rowNum);

            if (IsRowEmpty(row, headers))
                continue;

            if (skipped < skip)
            {
                skipped++;
                continue;
            }

            if (take is { } limit && result.Count >= limit)
                break;

            var dict = new Dictionary<string, string?>(headers.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var (name, col) in headers)
            {
                var cellValue = row.Cell(col).GetString().Trim();
                dict[name] = string.IsNullOrEmpty(cellValue) ? null : cellValue;
            }
            result.Add(dict);
        }

        return result;
    }

    private static bool IsRowEmpty(IXLRow row, List<(string Name, int Column)> headers)
    {
        foreach (var (_, col) in headers)
        {
            var value = row.Cell(col).GetString();
            if (!string.IsNullOrWhiteSpace(value))
                return false;
        }
        return true;
    }
}
