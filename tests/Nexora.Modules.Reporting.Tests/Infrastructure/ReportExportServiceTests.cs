using System.Text;
using ClosedXML.Excel;
using FluentAssertions;
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

        text.Should().Contain("1234.5");
        text.Should().Contain("Alice");
    }

    [Fact]
    public void Export_ExcelFormat_TrLocale_FormatsDecimalWithCommaSeparator()
    {
        var service = new ReportExportService(new FakeLocaleContext(locale: "tr-TR", documentLanguage: "tr"));

        using var stream = service.Export(SampleRows(), "EXCEL", "test");
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();

        // Amount column (col 2) row 2 should be "1234,5" in tr-TR
        sheet.Cell(2, 2).GetString().Should().Be("1234,5");
    }

    [Fact]
    public void Export_ExcelFormat_EnUsLocale_FormatsDecimalWithDotSeparator()
    {
        var service = new ReportExportService(new FakeLocaleContext(locale: "en-US"));

        using var stream = service.Export(SampleRows(), "EXCEL", "test");
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();

        sheet.Cell(2, 2).GetString().Should().Be("1234.5");
    }

    [Fact]
    public void Export_PdfFormat_ProducesNonEmptyStream()
    {
        var service = new ReportExportService(new FakeLocaleContext(locale: "en-US", documentLanguage: "en"));

        using var stream = service.Export(SampleRows(), "PDF", "Test Report");

        stream.Length.Should().BeGreaterThan(0);
        var buffer = new byte[4];
        stream.Position = 0;
        _ = stream.Read(buffer, 0, 4);
        Encoding.ASCII.GetString(buffer).Should().Be("%PDF");
    }

    [Fact]
    public void Export_UnknownFormat_Throws()
    {
        var service = new ReportExportService(new FakeLocaleContext());
        var act = () => service.Export(SampleRows(), "XML", "test");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Export_InvalidLocale_FallsBackToEnUs()
    {
        var service = new ReportExportService(new FakeLocaleContext(locale: "zz-ZZ-invalid"));

        using var stream = service.Export(SampleRows(), "EXCEL", "test");
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();

        sheet.Cell(2, 2).GetString().Should().Be("1234.5");
    }

    [Fact]
    public void Export_CsvFormat_EmptyRows_ReturnsEmptyStream()
    {
        var service = new ReportExportService(new FakeLocaleContext());

        using var stream = service.Export([], "CSV", "test");

        stream.Length.Should().Be(0);
    }

    [Fact]
    public void Export_JsonFormat_ContainsAllRows()
    {
        var service = new ReportExportService(new FakeLocaleContext());

        using var stream = service.Export(SampleRows(), "JSON", "test");
        var text = new StreamReader(stream).ReadToEnd();

        text.Should().Contain("Alice");
        text.Should().Contain("Bob");
    }

    [Fact]
    public void GetContentType_UnknownFormat_ReturnsOctetStream()
    {
        ReportExportService.GetContentType("xml").Should().Be("application/octet-stream");
    }

    [Fact]
    public void GetFileExtension_KnownFormats_ReturnExpectedExtensions()
    {
        ReportExportService.GetFileExtension("CSV").Should().Be(".csv");
        ReportExportService.GetFileExtension("EXCEL").Should().Be(".xlsx");
        ReportExportService.GetFileExtension("PDF").Should().Be(".pdf");
        ReportExportService.GetFileExtension("JSON").Should().Be(".json");
    }
}
