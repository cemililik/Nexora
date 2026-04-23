namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>A single row-level validation finding returned by the validate step.</summary>
/// <param name="RowNumber">1-based row number from the source file (header row excluded).</param>
/// <param name="ErrorKey">Localization key describing the failure, e.g. <c>lockey_contacts_import_validation_email_required</c>.</param>
/// <param name="FieldName">Optional mapped Contact field that failed (e.g. <c>email</c>).</param>
public sealed record ContactImportValidationErrorDto(
    int RowNumber,
    string ErrorKey,
    string? FieldName);

/// <summary>
/// Result of the import pre-flight validation step. Errors are capped at the first
/// 100 findings to bound response size; <see cref="ErrorCount"/> always reports the total.
/// </summary>
public sealed record ContactImportValidationDto(
    int TotalRows,
    int ErrorCount,
    IReadOnlyList<ContactImportValidationErrorDto> Errors);
