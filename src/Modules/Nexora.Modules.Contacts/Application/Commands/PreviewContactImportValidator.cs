using FluentValidation;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>Validates preview input.</summary>
public sealed class PreviewContactImportValidator : AbstractValidator<PreviewContactImportCommand>
{
    private static readonly string[] ValidFormats = ["csv", "xlsx"];

    public PreviewContactImportValidator()
    {
        RuleFor(x => x.StorageKey)
            .NotEmpty().WithMessage("lockey_contacts_validation_import_storage_key_required");

        RuleFor(x => x.FileFormat)
            .NotEmpty().WithMessage("lockey_contacts_validation_import_format_required")
            .Must(f => ValidFormats.Contains(f.ToLowerInvariant()))
            .WithMessage("lockey_contacts_validation_import_format_invalid");
    }
}
