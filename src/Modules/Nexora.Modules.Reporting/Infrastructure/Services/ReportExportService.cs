using System.Globalization;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Nexora.Modules.Reporting.Infrastructure.Services;

/// <summary>
/// Exports report data to various formats (CSV, Excel, PDF, JSON).
/// </summary>
public sealed class ReportExportService
{
    /// <summary>Exports rows to the specified format and returns the file bytes.</summary>
    public byte[] Export(
        IReadOnlyList<Dictionary<string, object?>> rows,
        string format,
        string reportName)
    {
        return format.ToUpperInvariant() switch
        {
            "CSV" => ExportCsv(rows),
            "EXCEL" => ExportExcel(rows, reportName),
            "PDF" => ExportPdf(rows, reportName),
            "JSON" => ExportJson(rows),
            _ => throw new ArgumentException($"Unsupported format: {format}")
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

    private static byte[] ExportCsv(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        if (rows.Count == 0)
            return Encoding.UTF8.GetBytes(string.Empty);

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));

        var headers = rows[0].Keys.ToList();

        foreach (var header in headers)
            csv.WriteField(header);
        csv.NextRecord();

        foreach (var row in rows)
        {
            foreach (var header in headers)
                csv.WriteField(row.GetValueOrDefault(header)?.ToString() ?? string.Empty);
            csv.NextRecord();
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] ExportExcel(IReadOnlyList<Dictionary<string, object?>> rows, string reportName)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(reportName.Length > 31 ? reportName[..31] : reportName);

        if (rows.Count == 0)
        {
            using var emptyStream = new MemoryStream();
            workbook.SaveAs(emptyStream);
            return emptyStream.ToArray();
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
                worksheet.Cell(rowIdx + 2, col + 1).Value = value?.ToString() ?? string.Empty;
            }
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] ExportPdf(IReadOnlyList<Dictionary<string, object?>> rows, string reportName)
    {
        QuestPDF.Settings.License = LicenseType.Community;

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
                        content.Text("No data").FontSize(12).Italic();
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

                        // Header row
                        foreach (var header in headers)
                        {
                            table.Cell().Background(Colors.Grey.Lighten3)
                                .Padding(4).Text(header).FontSize(9).Bold();
                        }

                        // Data rows
                        foreach (var row in rows)
                        {
                            foreach (var header in headers)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(4).Text(row.GetValueOrDefault(header)?.ToString() ?? "").FontSize(8);
                            }
                        }
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ").FontSize(8);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span(" of ").FontSize(8);
                    text.TotalPages().FontSize(8);
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    private static byte[] ExportJson(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        return JsonSerializer.SerializeToUtf8Bytes(rows, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
