using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Contacts.Infrastructure.Jobs;

/// <summary>Parameters for a contact import job.</summary>
public sealed record ContactImportJobParams : JobParams
{
    public required string FileName { get; init; }
    public required string FileFormat { get; init; }
    public required string StorageKey { get; init; }
    public Guid? OrganizationIdGuid { get; init; }
    public Guid ImportJobId { get; init; }
}

/// <summary>
/// Background job that downloads the import file from MinIO via storage key,
/// parses CSV/Excel content, and imports contacts in batches.
/// Performs duplicate detection and reports progress.
/// </summary>
public sealed class ContactImportJob(
    ITenantContextAccessor tenantContextAccessor,
    ContactsDbContext dbContext,
    IFileStorageService fileStorageService,
    IOptions<StorageOptions> storageOptions,
    ILogger<ContactImportJob> logger) : NexoraJob<ContactImportJobParams>(tenantContextAccessor, logger)
{
    private const int BatchSize = 100;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(ContactImportJobParams parameters, CancellationToken ct)
    {
        var tenantId = Guid.Parse(parameters.TenantId);
        var orgId = parameters.OrganizationIdGuid ?? Guid.Empty;
        var importJobId = ImportJobId.From(parameters.ImportJobId);

        var importJob = await dbContext.ImportJobs.FindAsync([importJobId], ct);
        if (importJob is null)
        {
            logger.LogWarning("ImportJob {ImportJobId} not found, aborting", importJobId);
            return;
        }

        var opts = storageOptions.Value;
        var bucketName = $"{opts.BucketPrefix}-{tenantId}";

        logger.LogInformation(
            "Starting contact import from {FileName} ({Format}) using storage key {StorageKey}",
            parameters.FileName, parameters.FileFormat, parameters.StorageKey);

        try
        {
            // Download file content from MinIO using storage key
            var fileContent = await fileStorageService.GetObjectAsync(
                bucketName, parameters.StorageKey, ct);

            var rows = ParseFile(fileContent, parameters.FileFormat);
            var totalRows = rows.Count;
            var successCount = 0;
            var errorCount = 0;

            importJob.MarkProcessing(totalRows);
            await dbContext.SaveChangesAsync(ct);

            for (var i = 0; i < totalRows; i += BatchSize)
            {
                ct.ThrowIfCancellationRequested();

                var batch = rows.Skip(i).Take(BatchSize).ToList();

                foreach (var row in batch)
                {
                    try
                    {
                        var existingContact = !string.IsNullOrWhiteSpace(row.Email)
                            ? await dbContext.Contacts.FirstOrDefaultAsync(
                                c => c.TenantId == tenantId && c.Email == row.Email, ct)
                            : null;

                        if (existingContact is not null)
                        {
                            logger.LogDebug("Skipping duplicate contact with email {Email}", row.Email);
                            errorCount++;
                            continue;
                        }

                        var contactType = Enum.TryParse<ContactType>(row.Type, ignoreCase: true, out var parsedType)
                            ? parsedType
                            : ContactType.Individual;

                        var contact = Contact.Create(
                            tenantId, orgId, contactType,
                            row.FirstName, row.LastName, row.CompanyName,
                            row.Email, row.Phone, ContactSource.Import);

                        await dbContext.Contacts.AddAsync(contact, ct);
                        successCount++;
                    }
                    catch (DomainException ex)
                    {
                        logger.LogWarning(ex, "Domain validation failed for row {RowIndex}", i + batch.IndexOf(row));
                        errorCount++;
                    }
                    catch (FormatException ex)
                    {
                        logger.LogWarning(ex, "Failed to parse row {RowIndex}", i + batch.IndexOf(row));
                        errorCount++;
                    }
                    catch (ArgumentException ex)
                    {
                        logger.LogWarning(ex, "Invalid data in row {RowIndex}", i + batch.IndexOf(row));
                        errorCount++;
                    }
                    catch (InvalidOperationException ex)
                    {
                        logger.LogWarning(ex, "Failed to import row {RowIndex}", i + batch.IndexOf(row));
                        errorCount++;
                    }
                }

                await dbContext.SaveChangesAsync(ct);

                var processed = Math.Min(i + BatchSize, totalRows);
                importJob.UpdateProgress(processed, successCount, errorCount);
                await dbContext.SaveChangesAsync(ct);

                logger.LogInformation(
                    "Import progress: {Processed}/{Total} (success: {Success}, errors: {Errors})",
                    processed, totalRows, successCount, errorCount);
            }

            importJob.MarkCompleted();
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Contact import completed. Total: {Total}, Success: {Success}, Errors: {Errors}",
                totalRows, successCount, errorCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Contact import job {ImportJobId} failed", importJobId);
            importJob.MarkFailed(ex.Message);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private static List<ContactImportRow> ParseFile(byte[] content, string format)
    {
        return format.ToLowerInvariant() switch
        {
            "csv" => ParseCsv(content),
            "xlsx" => ParseXlsx(content),
            _ => throw new NotSupportedException($"Unsupported import format: {format}")
        };
    }

    private static List<ContactImportRow> ParseCsv(byte[] content)
    {
        using var reader = new StreamReader(new MemoryStream(content));
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            HeaderValidated = null,
            MissingFieldFound = null,
        });

        return csv.GetRecords<ContactImportRow>().ToList();
    }

    private static List<ContactImportRow> ParseXlsx(byte[] content)
    {
        using var workbook = new XLWorkbook(new MemoryStream(content));
        var worksheet = workbook.Worksheet(1);
        var rows = new List<ContactImportRow>();

        var headerRow = worksheet.Row(1);
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var lastHeaderCell = headerRow.LastCellUsed();
        if (lastHeaderCell is null)
            return rows;

        for (var col = 1; col <= lastHeaderCell.Address.ColumnNumber; col++)
        {
            var headerValue = headerRow.Cell(col).GetString().Trim();
            if (!string.IsNullOrEmpty(headerValue))
                headers[headerValue] = col;
        }

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var rowNum = 2; rowNum <= lastRow; rowNum++)
        {
            var row = worksheet.Row(rowNum);

            rows.Add(new ContactImportRow
            {
                Type = GetCellValue(row, headers, "Type"),
                FirstName = GetCellValue(row, headers, "FirstName"),
                LastName = GetCellValue(row, headers, "LastName"),
                CompanyName = GetCellValue(row, headers, "CompanyName"),
                Email = GetCellValue(row, headers, "Email"),
                Phone = GetCellValue(row, headers, "Phone"),
                Title = GetCellValue(row, headers, "Title"),
            });
        }

        return rows;
    }

    private static string? GetCellValue(IXLRow row, Dictionary<string, int> headers, string columnName)
    {
        if (!headers.TryGetValue(columnName, out var colIndex))
            return null;

        var value = row.Cell(colIndex).GetString().Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}

/// <summary>Represents a single row from a contact import file.</summary>
public sealed record ContactImportRow
{
    public string? Type { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? CompanyName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Title { get; init; }
}
