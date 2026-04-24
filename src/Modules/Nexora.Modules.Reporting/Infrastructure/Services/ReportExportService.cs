using System.Globalization;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Nexora.SharedKernel.Abstractions.Localization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Nexora.Modules.Reporting.Infrastructure.Services;

/// <summary>
/// Exports report data to various formats (CSV, Excel, PDF, JSON).
/// CSV and JSON use invariant culture (machine-readable). Excel and PDF use the
/// tenant/user locale from <see cref="ILocaleContext"/> for number/date formatting,
/// and PDF labels are resolved for the tenant document language.
/// </summary>
public sealed class ReportExportService(ILocaleContext localeContext)
{
    /// <summary>Exports rows to the specified format and returns a seekable stream. Caller owns the stream.</summary>
    public Stream Export(
        IReadOnlyList<Dictionary<string, object?>> rows,
        string format,
        string reportName,
        string noDataLabel = "lockey_reporting_no_data")
    {
        ArgumentException.ThrowIfNullOrEmpty(format, nameof(format));

        return format.ToUpperInvariant() switch
        {
            "CSV" => ExportCsv(rows),
            "EXCEL" => ExportExcel(rows, reportName, ResolveCulture()),
            "PDF" => ExportPdf(rows, reportName, noDataLabel, ResolveCulture(), localeContext.DocumentLanguage),
            "JSON" => ExportJson(rows),
            _ => throw new ArgumentException($"Unsupported format: {format}", nameof(format))
        };
    }

    /// <summary>Returns the content type for the specified format.</summary>
    public static string GetContentType(string format) => format.ToUpperInvariant() switch
    {
        "CSV" => "text/csv",
        "EXCEL" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "PDF" => "application/pdf",
        "JSON" => "application/json",
        _ => "application/octet-stream"
    };

    /// <summary>Returns the file extension for the specified format.</summary>
    public static string GetFileExtension(string format) => format.ToUpperInvariant() switch
    {
        "CSV" => ".csv",
        "EXCEL" => ".xlsx",
        "PDF" => ".pdf",
        "JSON" => ".json",
        _ => ".bin"
    };

    private CultureInfo ResolveCulture()
    {
        // Belt-and-suspenders: try the tenant's configured locale, then en-US,
        // then InvariantCulture. The final fallback is load-bearing — if the
        // runtime is in globalization-invariant mode (e.g. an Alpine-based
        // container that was missing icu-libs), even `en-US` will throw
        // CultureNotFoundException. InvariantCulture always exists.
        return TryGetCulture(localeContext.Locale)
            ?? TryGetCulture("en-US")
            ?? CultureInfo.InvariantCulture;

        static CultureInfo? TryGetCulture(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            try { return CultureInfo.GetCultureInfo(name); }
            catch (CultureNotFoundException) { return null; }
        }
    }

    private static string FormatValue(object? value, CultureInfo culture) => value switch
    {
        null => string.Empty,
        string s => s,
        IFormattable f => f.ToString(null, culture),
        _ => value.ToString() ?? string.Empty
    };

    private static Stream ExportCsv(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        if (rows.Count == 0)
        {
            var emptyStream = new MemoryStream(Encoding.UTF8.GetBytes(string.Empty));
            emptyStream.Position = 0;
            return emptyStream;
        }

        var stream = new MemoryStream();
        var success = false;
        try
        {
            using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));

            var headers = rows[0].Keys.ToList();

            foreach (var header in headers)
                csv.WriteField(header);
            csv.NextRecord();

            foreach (var row in rows)
            {
                foreach (var header in headers)
                    csv.WriteField(FormatValue(row.GetValueOrDefault(header), CultureInfo.InvariantCulture));
                csv.NextRecord();
            }

            success = true;
        }
        finally
        {
            if (!success)
                stream.Dispose();
        }

        stream.Position = 0;
        return stream;
    }

    private static Stream ExportExcel(IReadOnlyList<Dictionary<string, object?>> rows, string reportName, CultureInfo culture)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(reportName.Length > 31 ? reportName[..31] : reportName);

        if (rows.Count == 0)
        {
            var emptyStream = new MemoryStream();
            workbook.SaveAs(emptyStream);
            emptyStream.Position = 0;
            return emptyStream;
        }

        var headers = rows[0].Keys.ToList();

        for (var col = 0; col < headers.Count; col++)
        {
            worksheet.Cell(1, col + 1).Value = headers[col];
            worksheet.Cell(1, col + 1).Style.Font.Bold = true;
        }

        for (var rowIdx = 0; rowIdx < rows.Count; rowIdx++)
        {
            for (var col = 0; col < headers.Count; col++)
            {
                var value = rows[rowIdx].GetValueOrDefault(headers[col]);
                worksheet.Cell(rowIdx + 2, col + 1).Value = FormatValue(value, culture);
            }
        }

        worksheet.Columns().AdjustToContents();

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static Stream ExportPdf(
        IReadOnlyList<Dictionary<string, object?>> rows,
        string reportName,
        string noDataLabel,
        CultureInfo culture,
        string documentLanguage)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var pageLabel = GetPdfLabel("lockey_reporting_pdf_page", documentLanguage);
        var ofLabel = GetPdfLabel("lockey_reporting_pdf_of", documentLanguage);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);

                page.Header().Text(reportName).FontSize(16).Bold().AlignCenter();

                page.Content().PaddingVertical(10).Element(content =>
                {
                    if (rows.Count == 0)
                    {
                        content.Text(noDataLabel).FontSize(12).Italic();
                        return;
                    }

                    var headers = rows[0].Keys.ToList();
                    var columnCount = (uint)headers.Count;

                    content.Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            for (var i = 0; i < columnCount; i++)
                                columns.RelativeColumn();
                        });

                        foreach (var header in headers)
                        {
                            table.Cell().Background(Colors.Grey.Lighten3)
                                .Padding(4).Text(header).FontSize(9).Bold();
                        }

                        foreach (var row in rows)
                        {
                            foreach (var header in headers)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(4).Text(FormatValue(row.GetValueOrDefault(header), culture)).FontSize(8);
                            }
                        }
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span($"{pageLabel} ").FontSize(8);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span($" {ofLabel} ").FontSize(8);
                    text.TotalPages().FontSize(8);
                });
            });
        });

        var stream = new MemoryStream();
        document.GeneratePdf(stream);
        stream.Position = 0;
        return stream;
    }

    private static Stream ExportJson(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, rows, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        stream.Position = 0;
        return stream;
    }

    // Backend-rendered PDF labels. Frontend `lockey_` resolution does not apply here —
    // generated documents embed translated text at generation time.
    private static string GetPdfLabel(string key, string language) => (key, language) switch
    {
        ("lockey_reporting_pdf_page", "tr") => "Sayfa",
        ("lockey_reporting_pdf_of", "tr") => "/",
        ("lockey_reporting_pdf_page", _) => "Page",
        ("lockey_reporting_pdf_of", _) => "of",
        _ => key
    };
}
