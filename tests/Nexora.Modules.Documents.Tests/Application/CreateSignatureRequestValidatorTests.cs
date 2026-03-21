using Nexora.Modules.Documents.Application.Commands;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class CreateSignatureRequestValidatorTests
{
    private readonly CreateSignatureRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var command = new CreateSignatureRequestCommand(
            Guid.NewGuid(), "Please sign", null,
            [new SignatureRecipientInput(Guid.NewGuid(), "signer@test.com", "Signer", 1)]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyDocumentId_FailsValidation()
    {
        var command = new CreateSignatureRequestCommand(
            Guid.Empty, "Title", null,
            [new SignatureRecipientInput(Guid.NewGuid(), "signer@test.com", "Signer", 1)]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DocumentId");
    }

    [Fact]
    public void Validate_EmptyTitle_FailsValidation()
    {
        var command = new CreateSignatureRequestCommand(
            Guid.NewGuid(), "", null,
            [new SignatureRecipientInput(Guid.NewGuid(), "signer@test.com", "Signer", 1)]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title");
    }

    [Fact]
    public void Validate_EmptyRecipients_FailsValidation()
    {
        var command = new CreateSignatureRequestCommand(
            Guid.NewGuid(), "Title", null, []);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Recipients");
    }

    [Fact]
    public void Validate_InvalidRecipientEmail_FailsValidation()
    {
        var command = new CreateSignatureRequestCommand(
            Guid.NewGuid(), "Title", null,
            [new SignatureRecipientInput(Guid.NewGuid(), "not-an-email", "Signer", 1)]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ZeroSigningOrder_FailsValidation()
    {
        var command = new CreateSignatureRequestCommand(
            Guid.NewGuid(), "Title", null,
            [new SignatureRecipientInput(Guid.NewGuid(), "signer@test.com", "Signer", 0)]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }
}
