using FluentValidation;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>Validates contact import input.</summary>
public sealed class StartContactImportValidator : AbstractValidator<StartContactImportCommand>
{
    private static readonly string[] ValidFormats = ["csv", "xlsx"];

    public StartContactImportValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("lockey_contacts_validation_import_filename_required");

        RuleFor(x => x.FileFormat)
            .NotEmpty().WithMessage("lockey_contacts_validation_import_format_required")
            .Must(f => ValidFormats.Contains(f.ToLowerInvariant()))
            .WithMessage("lockey_contacts_validation_import_format_invalid");

        RuleFor(x => x.StorageKey)
            .NotEmpty().WithMessage("lockey_contacts_validation_import_storage_key_required");
    }
}
