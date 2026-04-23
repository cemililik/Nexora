using System.Text.RegularExpressions;
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
    private const string FirstNameField = "firstName";
    private const string LastNameField = "lastName";
    private const string CompanyNameField = "companyName";

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private static readonly Regex PhoneRegex = new(
        @"^\+?[0-9\s().-]{5,}$", RegexOptions.Compiled);

    public async Task<Result<ContactImportValidationDto>> Handle(
        ValidateContactImportCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
        {
            logger.LogWarning("Contact import validation rejected — invalid tenant context");
            return Result<ContactImportValidationDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_invalid_tenant_context"));
        }

        if (tenantContextAccessor.Current.TryGetOrganizationGuid() is not { } orgId)
        {
            logger.LogWarning(
                "Contact import validation rejected — invalid organization context for tenant {TenantId}",
                tenantId);
            return Result<ContactImportValidationDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_invalid_organization_context"));
        }

        var expectedPrefix = $"{orgId}/contacts/imports/";
        if (!request.StorageKey.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Invalid storage key prefix on import validate for tenant {TenantId} organization {OrganizationId}",
                tenantId, orgId);
            return Result<ContactImportValidationDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_import_invalid_storage_key"));
        }

        var opts = storageOptions.Value;
        var bucketName = $"{opts.BucketPrefix}-{tenantId}";

        var exists = await fileStorageService.ObjectExistsAsync(
            bucketName, request.StorageKey, cancellationToken);
        if (!exists)
        {
            logger.LogWarning(
                "Contact import validation rejected — file not found for tenant {TenantId} organization {OrganizationId}",
                tenantId, orgId);
            return Result<ContactImportValidationDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_import_file_not_found"));
        }

        var content = await fileStorageService.GetObjectAsync(
            bucketName, request.StorageKey, cancellationToken);

        using var headerStream = new MemoryStream(content);
        var headers = ContactImportParser.ParseHeaders(headerStream, request.FileFormat);
        using var rowsStream = new MemoryStream(content);
        var rows = ContactImportParser.ParseRows(rowsStream, request.FileFormat);

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
            if (string.IsNullOrWhiteSpace(target) || target == ImportColumnMapping.SkipSentinel)
                continue;
            targetToSource[target] = source;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var rowNumber = i + 1;
            var row = rows[i];

            var email = GetMappedValue(row, targetToSource, EmailField);
            var phone = GetMappedValue(row, targetToSource, PhoneField);
            var firstName = GetMappedValue(row, targetToSource, FirstNameField);
            var lastName = GetMappedValue(row, targetToSource, LastNameField);
            var companyName = GetMappedValue(row, targetToSource, CompanyNameField);

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

            // A contact must have at least a first/last name OR a company name.
            var hasPersonName = !string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName);
            var hasCompany = !string.IsNullOrWhiteSpace(companyName);
            if (!hasPersonName && !hasCompany)
            {
                totalErrorCount++;
                TryAddError(errors, new ContactImportValidationErrorDto(
                    rowNumber,
                    "lockey_contacts_import_validation_name_or_company_required",
                    FirstNameField));
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
