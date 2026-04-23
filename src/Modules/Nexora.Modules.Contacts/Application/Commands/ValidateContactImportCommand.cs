using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Infrastructure.Parsers;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>
/// Pre-flight validation for a contact import. Parses all rows, applies the supplied
/// column mapping, and returns row-level error findings (capped at 100).
/// Does NOT perform duplicate detection — that is deferred to the background job.
/// </summary>
public sealed record ValidateContactImportCommand(
    string StorageKey,
    string FileFormat,
    IReadOnlyDictionary<string, string> ColumnMapping) : ICommand<ContactImportValidationDto>;

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

/// <summary>Runs the row-level validation rules and returns a capped error report.</summary>
public sealed class ValidateContactImportHandler(
    IFileStorageService fileStorageService,
    ITenantContextAccessor tenantContextAccessor,
    IOptions<StorageOptions> storageOptions,
    ILogger<ValidateContactImportHandler> logger)
    : ICommandHandler<ValidateContactImportCommand, ContactImportValidationDto>
{
    private const int MaxReportedErrors = 100;
    private const string EmailField = "email";
    private const string PhoneField = "phone";

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private static readonly Regex PhoneRegex = new(
        @"^\+?[0-9\s().-]{5,}$", RegexOptions.Compiled);

    public async Task<Result<ContactImportValidationDto>> Handle(
        ValidateContactImportCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<ContactImportValidationDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_invalid_tenant_context"));

        if (tenantContextAccessor.Current.TryGetOrganizationGuid() is not { } orgId)
            return Result<ContactImportValidationDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_invalid_organization_context"));

        var expectedPrefix = $"{orgId}/contacts/imports/";
        if (!request.StorageKey.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Validate rejected: storage key {StorageKey} does not match expected prefix for organization {OrganizationId}",
                request.StorageKey, orgId);
            return Result<ContactImportValidationDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_import_invalid_storage_key"));
        }

        var opts = storageOptions.Value;
        var bucketName = $"{opts.BucketPrefix}-{tenantId}";

        var exists = await fileStorageService.ObjectExistsAsync(
            bucketName, request.StorageKey, cancellationToken);
        if (!exists)
            return Result<ContactImportValidationDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_import_file_not_found"));

        var content = await fileStorageService.GetObjectAsync(
            bucketName, request.StorageKey, cancellationToken);

        var headers = ContactImportParser.ParseHeaders(content, request.FileFormat);
        var rows = ContactImportParser.ParseRows(content, request.FileFormat);

        var errors = new List<ContactImportValidationErrorDto>();
        var totalErrorCount = 0;

        // Batch-level: mapping referencing unknown source columns.
        var headerSet = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);
        foreach (var sourceColumn in request.ColumnMapping.Keys)
        {
            if (!headerSet.Contains(sourceColumn))
            {
                totalErrorCount++;
                if (errors.Count < MaxReportedErrors)
                {
                    errors.Add(new ContactImportValidationErrorDto(
                        RowNumber: 0,
                        ErrorKey: "lockey_contacts_import_validation_unknown_source_column",
                        FieldName: sourceColumn));
                }
            }
        }

        // Build inverse mapping: Contact target field -> source header.
        var targetToSource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (source, target) in request.ColumnMapping)
        {
            if (string.IsNullOrWhiteSpace(target) || target == "__skip__")
                continue;
            targetToSource[target] = source;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var rowNumber = i + 1;
            var row = rows[i];

            var email = GetMappedValue(row, targetToSource, EmailField);
            var phone = GetMappedValue(row, targetToSource, PhoneField);

            if (string.IsNullOrWhiteSpace(email))
            {
                totalErrorCount++;
                TryAddError(errors, new ContactImportValidationErrorDto(
                    rowNumber,
                    "lockey_contacts_import_validation_email_required",
                    EmailField));
            }
            else if (!EmailRegex.IsMatch(email))
            {
                totalErrorCount++;
                TryAddError(errors, new ContactImportValidationErrorDto(
                    rowNumber,
                    "lockey_contacts_import_validation_email_invalid",
                    EmailField));
            }

            if (!string.IsNullOrWhiteSpace(phone) && !PhoneRegex.IsMatch(phone))
            {
                totalErrorCount++;
                TryAddError(errors, new ContactImportValidationErrorDto(
                    rowNumber,
                    "lockey_contacts_import_validation_phone_invalid",
                    PhoneField));
            }
        }

        logger.LogInformation(
            "Contact import validation completed for tenant {TenantId}: {TotalRows} rows, {ErrorCount} errors",
            tenantId, rows.Count, totalErrorCount);

        var dto = new ContactImportValidationDto(rows.Count, totalErrorCount, errors);
        return Result<ContactImportValidationDto>.Success(
            dto, LocalizedMessage.Of("lockey_contacts_import_validation_completed"));
    }

    private static string? GetMappedValue(
        IReadOnlyDictionary<string, string?> row,
        IReadOnlyDictionary<string, string> targetToSource,
        string targetField)
    {
        if (!targetToSource.TryGetValue(targetField, out var sourceColumn))
            return null;
        return row.TryGetValue(sourceColumn, out var value) ? value : null;
    }

    private static void TryAddError(List<ContactImportValidationErrorDto> errors, ContactImportValidationErrorDto error)
    {
        if (errors.Count < MaxReportedErrors)
            errors.Add(error);
    }
}
