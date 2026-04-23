using FluentValidation;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>Validates the validate-step request body.</summary>
public sealed class ValidateContactImportValidator : AbstractValidator<ValidateContactImportCommand>
{
    private static readonly string[] ValidFormats = ["csv", "xlsx"];

    public ValidateContactImportValidator()
    {
        RuleFor(x => x.StorageKey)
            .NotEmpty().WithMessage("lockey_contacts_validation_import_storage_key_required");

        RuleFor(x => x.FileFormat)
            .NotEmpty().WithMessage("lockey_contacts_validation_import_format_required")
            .Must(f => ValidFormats.Contains(f.ToLowerInvariant()))
            .WithMessage("lockey_contacts_validation_import_format_invalid");

        RuleFor(x => x.ColumnMapping)
            .NotNull().WithMessage("lockey_contacts_validation_import_mapping_required");
    }
}
