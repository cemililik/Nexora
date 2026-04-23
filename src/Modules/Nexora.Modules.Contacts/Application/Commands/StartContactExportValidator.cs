using FluentValidation;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>Validates contact export input.</summary>
public sealed class StartContactExportValidator : AbstractValidator<StartContactExportCommand>
{
    /// <summary>Allowed output formats for contact export.</summary>
    public static readonly IReadOnlySet<string> ValidFormats =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "csv", "xlsx", "vcard" };

    /// <summary>Date fields that may be used as a range filter.</summary>
    public static readonly IReadOnlySet<string> ValidDateFields =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CreatedAt", "UpdatedAt" };

    /// <summary>Core contact fields the user may include in the export.</summary>
    public static readonly IReadOnlySet<string> AllowedCoreFields =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "firstName", "lastName", "displayName",
            "email", "phone", "mobile", "website",
            "companyName", "taxId", "title",
            "type", "status", "source",
            "language", "currency",
            "createdAt", "updatedAt"
        };

    /// <summary>Initialises validation rules.</summary>
    public StartContactExportValidator()
    {
        RuleFor(x => x.Format)
            .NotEmpty().WithMessage("lockey_contacts_validation_export_format_required")
            .Must(f => !string.IsNullOrWhiteSpace(f) && ValidFormats.Contains(f))
            .WithMessage("lockey_contacts_validation_export_format_invalid");

        RuleFor(x => x.DateField!)
            .Must(f => ValidDateFields.Contains(f))
            .When(x => !string.IsNullOrWhiteSpace(x.DateField))
            .WithMessage("lockey_contacts_validation_export_date_field_invalid");

        RuleFor(x => x)
            .Must(x => !(x.DateFrom.HasValue && x.DateTo.HasValue) || x.DateFrom <= x.DateTo)
            .WithMessage("lockey_contacts_validation_export_date_range_invalid");

        RuleFor(x => x.Fields!)
            .Must(fields => fields.All(f => AllowedCoreFields.Contains(f)))
            .When(x => x.Fields is { Count: > 0 })
            .WithMessage("lockey_contacts_validation_export_fields_invalid");
    }
}
