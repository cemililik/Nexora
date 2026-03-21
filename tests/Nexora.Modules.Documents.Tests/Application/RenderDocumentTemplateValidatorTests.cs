using Nexora.Modules.Documents.Application.Commands;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class RenderDocumentTemplateValidatorTests
{
    private readonly RenderDocumentTemplateValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var command = new RenderDocumentTemplateCommand(Guid.NewGuid(), Guid.NewGuid(), "output.pdf", new());

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyTemplateId_FailsValidation()
    {
        var command = new RenderDocumentTemplateCommand(Guid.Empty, Guid.NewGuid(), "output.pdf", new());

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TemplateId");
    }

    [Fact]
    public void Validate_EmptyFolderId_FailsValidation()
    {
        var command = new RenderDocumentTemplateCommand(Guid.NewGuid(), Guid.Empty, "output.pdf", new());

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FolderId");
    }

    [Fact]
    public void Validate_EmptyOutputName_FailsValidation()
    {
        var command = new RenderDocumentTemplateCommand(Guid.NewGuid(), Guid.NewGuid(), "", new());

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "OutputName");
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("path/to/file.pdf")]
    [InlineData("path\\to\\file.pdf")]
    public void Validate_PathTraversalInOutputName_FailsValidation(string outputName)
    {
        var command = new RenderDocumentTemplateCommand(Guid.NewGuid(), Guid.NewGuid(), outputName, new());

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "OutputName");
    }

    [Fact]
    public void Validate_OutputNameExceedsMaxLength_FailsValidation()
    {
        var command = new RenderDocumentTemplateCommand(Guid.NewGuid(), Guid.NewGuid(), new string('a', 501), new());

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "OutputName");
    }
}
