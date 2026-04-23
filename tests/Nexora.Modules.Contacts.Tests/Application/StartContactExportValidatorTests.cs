using Nexora.Modules.Contacts.Application.Commands;

namespace Nexora.Modules.Contacts.Tests.Application;

public sealed class StartContactExportValidatorTests
{
    private readonly StartContactExportValidator _validator = new();

    [Theory]
    [InlineData("csv")]
    [InlineData("xlsx")]
    [InlineData("vcard")]
    [InlineData("CSV")]
    [InlineData("VCARD")]
    public void Validate_ValidFormat_ShouldPass(string format)
    {
        var command = new StartContactExportCommand(format);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("json")]
    [InlineData("xml")]
    [InlineData("pdf")]
    [InlineData("txt")]
    public void Validate_InvalidFormat_ShouldFail(string format)
    {
        var command = new StartContactExportCommand(format);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "lockey_contacts_validation_export_format_invalid");
    }

    [Fact]
    public void Validate_EmptyFormat_ShouldFail()
    {
        var command = new StartContactExportCommand("");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "lockey_contacts_validation_export_format_required");
    }

    [Theory]
    [InlineData("CreatedAt")]
    [InlineData("UpdatedAt")]
    [InlineData("createdat")]
    public void Validate_ValidDateField_ShouldPass(string dateField)
    {
        var command = new StartContactExportCommand("csv", DateField: dateField);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("ArchivedAt")]
    [InlineData("DeletedAt")]
    public void Validate_InvalidDateField_ShouldFail(string dateField)
    {
        var command = new StartContactExportCommand("csv", DateField: dateField);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "lockey_contacts_validation_export_date_field_invalid");
    }

    [Fact]
    public void Validate_DateFromAfterDateTo_ShouldFail()
    {
        var command = new StartContactExportCommand(
            "csv",
            DateFrom: DateTimeOffset.UtcNow,
            DateTo: DateTimeOffset.UtcNow.AddDays(-1));
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "lockey_contacts_validation_export_date_range_invalid");
    }

    [Fact]
    public void Validate_DateFromBeforeDateTo_ShouldPass()
    {
        var command = new StartContactExportCommand(
            "csv",
            DateFrom: DateTimeOffset.UtcNow.AddDays(-7),
            DateTo: DateTimeOffset.UtcNow);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidCoreFields_ShouldPass()
    {
        var command = new StartContactExportCommand(
            "csv", Fields: new[] { "firstName", "lastName", "email", "createdAt" });
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_UnknownCoreField_ShouldFail()
    {
        var command = new StartContactExportCommand(
            "csv", Fields: new[] { "firstName", "socialSecurityNumber" });
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "lockey_contacts_validation_export_fields_invalid");
    }
}
