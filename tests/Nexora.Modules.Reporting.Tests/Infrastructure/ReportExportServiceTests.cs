using System.Text;
using ClosedXML.Excel;
using Nexora.Modules.Reporting.Infrastructure.Services;
using Nexora.SharedKernel.Abstractions.Localization;

namespace Nexora.Modules.Reporting.Tests.Infrastructure;

public sealed class ReportExportServiceTests
{
    private sealed class FakeLocaleContext(
        string locale = "en-US",
        string documentLanguage = "en") : ILocaleContext
    {
        public string Language => documentLanguage;
        public string Locale => locale;
        public string Currency => "USD";
        public string Timezone => "UTC";
        public string DocumentLanguage => documentLanguage;
    }

    private static List<Dictionary<string, object?>> SampleRows() =>
    [
        new() { ["Name"] = "Alice", ["Amount"] = 1234.5m, ["When"] = new DateTime(2026, 4, 18) },
        new() { ["Name"] = "Bob",   ["Amount"] = 99.9m,  ["When"] = new DateTime(2026, 4, 19) }
    ];

    [Fact]
    public void Export_CsvFormat_UsesInvariantCulture_RegardlessOfLocale()
    {
        var service = new ReportExportService(new FakeLocaleContext(locale: "tr-TR"));

        using var stream = service.Export(SampleRows(), "CSV", "test");
        var text = new StreamReader(stream, Encoding.UTF8).ReadToEnd();

        // Invariant: decimal separator is "."
        Assert.Contains("1234.5", text);
        Assert.Contains("Alice", text);
    }

    [Fact]
    public void Export_ExcelFormat_TrLocale_FormatsDecimalWithCommaSeparator()
    {
        var service = new ReportExportService(new FakeLocaleContext(locale: "tr-TR", documentLanguage: "tr"));

        using var stream = service.Export(SampleRows(), "EXCEL", "test");
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();

        // Amount column (col 2) row 2 should be "1234,5" in tr-TR
        var cell = sheet.Cell(2, 2).GetString();
        Assert.Equal("1234,5", cell);
    }

    [Fact]
    public void Export_ExcelFormat_EnUsLocale_FormatsDecimalWithDotSeparator()
    {
        var service = new ReportExportService(new FakeLocaleContext(locale: "en-US"));

        using var stream = service.Export(SampleRows(), "EXCEL", "test");
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();

        Assert.Equal("1234.5", sheet.Cell(2, 2).GetString());
    }

    [Fact]
    public void Export_PdfFormat_ProducesNonEmptyStream()
    {
        var service = new ReportExportService(new FakeLocaleContext(locale: "en-US", documentLanguage: "en"));

        using var stream = service.Export(SampleRows(), "PDF", "Test Report");

        Assert.True(stream.Length > 0);
        // PDF magic bytes
        var buffer = new byte[4];
        stream.Position = 0;
        _ = stream.Read(buffer, 0, 4);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(buffer));
    }

    [Fact]
    public void Export_UnknownFormat_Throws()
    {
        var service = new ReportExportService(new FakeLocaleContext());

        Assert.Throws<ArgumentException>(() => service.Export(SampleRows(), "XML", "test"));
    }

    [Fact]
    public void Export_InvalidLocale_FallsBackToEnUs()
    {
        var service = new ReportExportService(new FakeLocaleContext(locale: "zz-ZZ-invalid"));

        using var stream = service.Export(SampleRows(), "EXCEL", "test");
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();

        Assert.Equal("1234.5", sheet.Cell(2, 2).GetString());
    }

    [Fact]
    public void Export_CsvFormat_EmptyRows_ReturnsEmptyStream()
    {
        var service = new ReportExportService(new FakeLocaleContext());

        using var stream = service.Export([], "CSV", "test");

        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void Export_JsonFormat_ContainsAllRows()
    {
        var service = new ReportExportService(new FakeLocaleContext());

        using var stream = service.Export(SampleRows(), "JSON", "test");
        var text = new StreamReader(stream).ReadToEnd();

        Assert.Contains("Alice", text);
        Assert.Contains("Bob", text);
    }

    [Fact]
    public void GetContentType_UnknownFormat_ReturnsOctetStream()
    {
        Assert.Equal("application/octet-stream", ReportExportService.GetContentType("xml"));
    }

    [Fact]
    public void GetFileExtension_KnownFormats_ReturnExpectedExtensions()
    {
        Assert.Equal(".csv", ReportExportService.GetFileExtension("CSV"));
        Assert.Equal(".xlsx", ReportExportService.GetFileExtension("EXCEL"));
        Assert.Equal(".pdf", ReportExportService.GetFileExtension("PDF"));
        Assert.Equal(".json", ReportExportService.GetFileExtension("JSON"));
    }
}
