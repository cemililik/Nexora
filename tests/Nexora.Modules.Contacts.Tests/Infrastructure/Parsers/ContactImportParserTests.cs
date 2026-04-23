using System.Text;
using ClosedXML.Excel;
using FluentAssertions;
using Nexora.Modules.Contacts.Infrastructure.Parsers;

namespace Nexora.Modules.Contacts.Tests.Infrastructure.Parsers;

/// <summary>Unit tests for <see cref="ContactImportParser"/>.</summary>
public sealed class ContactImportParserTests
{
    [Fact]
    public void ParseHeaders_Csv_ReturnsDetectedHeaders()
    {
        var csv = "First Name,Last Name,Email\nAda,Lovelace,ada@example.com\n";
        var bytes = Encoding.UTF8.GetBytes(csv);

        var headers = ContactImportParser.ParseHeaders(bytes, "csv");

        headers.Should().BeEquivalentTo(new[] { "First Name", "Last Name", "Email" });
    }

    [Fact]
    public void ParseRows_Csv_ProjectsRowsKeyedByHeader()
    {
        var csv = "FirstName,Email,Phone\nAda,ada@example.com,+1234\nGrace,grace@example.com,\n";
        var bytes = Encoding.UTF8.GetBytes(csv);

        var rows = ContactImportParser.ParseRows(bytes, "csv");

        rows.Should().HaveCount(2);
        rows[0]["FirstName"].Should().Be("Ada");
        rows[0]["Email"].Should().Be("ada@example.com");
        rows[0]["Phone"].Should().Be("+1234");
        rows[1]["Phone"].Should().BeNull();
    }

    [Fact]
    public void ParseRows_CsvWithUnclosedQuote_Throws()
    {
        // Unclosed quote produces a parser-level failure; callers convert to Result.Failure.
        var csv = "FirstName,Email\n\"Ada,ada@example.com\n";
        var bytes = Encoding.UTF8.GetBytes(csv);

        var act = () => ContactImportParser.ParseRows(bytes, "csv");

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseHeaders_Xlsx_ReturnsHeadersFromFirstRow()
    {
        var bytes = BuildXlsx(
            ["FirstName", "LastName", "Email"],
            [["Ada", "Lovelace", "ada@example.com"]]);

        var headers = ContactImportParser.ParseHeaders(bytes, "xlsx");

        headers.Should().BeEquivalentTo(new[] { "FirstName", "LastName", "Email" });
    }

    [Fact]
    public void ParseRows_Xlsx_SkipsEmptyTrailingRows()
    {
        var bytes = BuildXlsx(
            ["FirstName", "Email"],
            [
                ["Ada", "ada@example.com"],
                ["Grace", "grace@example.com"],
                ["", ""],
            ]);

        var rows = ContactImportParser.ParseRows(bytes, "xlsx");

        rows.Should().HaveCount(2);
        rows[0]["FirstName"].Should().Be("Ada");
        rows[1]["FirstName"].Should().Be("Grace");
    }

    [Fact]
    public void ParseHeaders_CsvWithCaseOnlyDuplicate_ThrowsFormatException()
    {
        // "Email" and "email" differ only in case — this would silently collide in the
        // case-insensitive row dictionary, so the parser rejects the file up front.
        var csv = "FirstName,Email,email\nAda,a@x.com,b@x.com\n";
        var bytes = Encoding.UTF8.GetBytes(csv);

        var act = () => ContactImportParser.ParseHeaders(bytes, "csv");

        act.Should().Throw<FormatException>()
            .WithMessage("lockey_contacts_import_error_duplicate_headers");
    }

    [Fact]
    public void ParseRows_CsvWithCaseOnlyDuplicate_ThrowsFormatException()
    {
        var csv = "Email,EMAIL\na@x.com,b@x.com\n";
        var bytes = Encoding.UTF8.GetBytes(csv);

        var act = () => ContactImportParser.ParseRows(bytes, "csv");

        act.Should().Throw<FormatException>()
            .WithMessage("lockey_contacts_import_error_duplicate_headers");
    }

    [Fact]
    public void ParseRows_Csv_RespectsTakeLimit()
    {
        var csv = "Email\na@x.com\nb@x.com\nc@x.com\nd@x.com\n";
        var bytes = Encoding.UTF8.GetBytes(csv);

        var rows = ContactImportParser.ParseRows(bytes, "csv", skip: 0, take: 2);

        rows.Should().HaveCount(2);
        rows[0]["Email"].Should().Be("a@x.com");
        rows[1]["Email"].Should().Be("b@x.com");
    }

    private static byte[] BuildXlsx(string[] headers, IReadOnlyList<string[]> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = rows[r][c];
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
