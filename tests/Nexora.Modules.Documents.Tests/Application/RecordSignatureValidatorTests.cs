using Nexora.Modules.Documents.Application.Commands;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class RecordSignatureValidatorTests
{
    private readonly RecordSignatureValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var command = new RecordSignatureCommand(Guid.NewGuid(), Guid.NewGuid(), "signature-data", "127.0.0.1");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySignatureRequestId_FailsValidation()
    {
        var command = new RecordSignatureCommand(Guid.Empty, Guid.NewGuid(), "signature-data", "127.0.0.1");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SignatureRequestId");
    }

    [Fact]
    public void Validate_EmptyRecipientId_FailsValidation()
    {
        var command = new RecordSignatureCommand(Guid.NewGuid(), Guid.Empty, "signature-data", "127.0.0.1");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RecipientId");
    }

    [Fact]
    public void Validate_EmptySignatureData_FailsValidation()
    {
        var command = new RecordSignatureCommand(Guid.NewGuid(), Guid.NewGuid(), "", "127.0.0.1");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SignatureData");
    }

    [Fact]
    public void Validate_SignatureDataExceedsMaxLength_FailsValidation()
    {
        var command = new RecordSignatureCommand(Guid.NewGuid(), Guid.NewGuid(), new string('x', 500_001), "127.0.0.1");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SignatureData");
    }

    [Fact]
    public void Validate_EmptyIpAddress_FailsValidation()
    {
        var command = new RecordSignatureCommand(Guid.NewGuid(), Guid.NewGuid(), "signature-data", "");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "IpAddress");
    }

    [Fact]
    public void Validate_IpAddressExceedsMaxLength_FailsValidation()
    {
        var command = new RecordSignatureCommand(Guid.NewGuid(), Guid.NewGuid(), "signature-data", new string('1', 46));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "IpAddress");
    }

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("127.0.0.1; DROP TABLE")]
    [InlineData("<script>alert(1)</script>")]
    public void Validate_InvalidIpAddressFormat_FailsValidation(string ipAddress)
    {
        var command = new RecordSignatureCommand(Guid.NewGuid(), Guid.NewGuid(), "signature-data", ipAddress);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "IpAddress");
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("::1")]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334")]
    [InlineData("::ffff:192.0.2.1")]
    public void Validate_ValidIpAddressFormats_Passes(string ipAddress)
    {
        var command = new RecordSignatureCommand(Guid.NewGuid(), Guid.NewGuid(), "signature-data", ipAddress);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
