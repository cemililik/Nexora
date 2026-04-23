using System.ComponentModel;
using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.Localization;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Infrastructure.Jobs;

/// <summary>Parameters for a contact export job.</summary>
public sealed record ContactExportJobParams : JobParams
{
    /// <summary>Identifier of the persisted <see cref="ExportJob"/> record.</summary>
    public required Guid ExportJobId { get; init; }

    /// <summary>Output format (csv | xlsx | vcard).</summary>
    public required string Format { get; init; }

    /// <summary>Core contact fields the user selected. Null/empty = default set.</summary>
    public IReadOnlyList<string>? Fields { get; init; }

    /// <summary>Custom field definition IDs to include as additional columns.</summary>
    public IReadOnlyList<Guid>? CustomFieldIds { get; init; }

    /// <summary>Optional status filter applied to the source query.</summary>
    public string? StatusFilter { get; init; }

    /// <summary>Optional type filter applied to the source query.</summary>
    public string? TypeFilter { get; init; }

    /// <summary>Inclusive lower bound for the date filter.</summary>
    public DateTimeOffset? DateFrom { get; init; }

    /// <summary>Inclusive upper bound for the date filter.</summary>
    public DateTimeOffset? DateTo { get; init; }

    /// <summary>Date field the range applies to ("CreatedAt" or "UpdatedAt").</summary>
    public string? DateField { get; init; }

    /// <summary>User that initiated the export; receives the completion notification.</summary>
    public Guid? TriggeredByUserId { get; init; }

    /// <summary>Organization scope as a <see cref="Guid"/>.</summary>
    public Guid? OrganizationIdGuid { get; init; }
}

/// <summary>
/// Background job that materialises contacts matching the requested filters, produces
/// a CSV/XLSX/vCard file, uploads it to MinIO, and notifies the requester.
/// Runs on the <c>bulk</c> Hangfire queue per infrastructure standards.
/// </summary>
[Queue("bulk")]
[DisplayName("contacts:bulk-export")]
public sealed class ContactExportJob(
    ITenantContextAccessor tenantContextAccessor,
    ContactsDbContext dbContext,
    IFileStorageService fileStorageService,
    IOptions<StorageOptions> storageOptions,
    IOutbox outbox,
    INotificationService notificationService,
    ILocaleContext localeContext,
    ILogger<ContactExportJob> logger) : NexoraJob<ContactExportJobParams>(tenantContextAccessor, logger)
{
    private static readonly string[] DefaultCoreFields =
    [
        "firstName", "lastName", "email", "phone", "mobile",
        "companyName", "title", "type", "status", "createdAt"
    ];

    /// <inheritdoc />
    protected override async Task ExecuteAsync(ContactExportJobParams parameters, CancellationToken ct)
    {
        var tenantId = Guid.Parse(parameters.TenantId);
        var orgId = parameters.OrganizationIdGuid ?? Guid.Empty;
        var exportJobId = ExportJobId.From(parameters.ExportJobId);

        var exportJob = await dbContext.ExportJobs.FindAsync([exportJobId], ct);
        if (exportJob is null)
        {
            logger.LogWarning("ExportJob {ExportJobId} not found, aborting", exportJobId);
            return;
        }

        // Idempotency guard — skip Completed or Failed terminal states; Processing means
        // a mid-job Hangfire retry and must resume without re-running MarkProcessing,
        // re-emitting the outbox event or re-sending the completion notification.
        if (exportJob.Status is ExportJobStatus.Completed or ExportJobStatus.Failed)
        {
            logger.LogWarning(
                "ExportJob {ExportJobId} already in terminal state {Status}, skipping",
                exportJobId, exportJob.Status);
            return;
        }
        var startedFromQueued = exportJob.Status == ExportJobStatus.Queued;

        List<Contact> contacts;
        Dictionary<Guid, List<ContactCustomField>> customFieldsByContactId;
        Dictionary<Guid, string> customFieldNames;

        {
            var query = dbContext.Contacts
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.OrganizationId == orgId);

            if (!string.IsNullOrWhiteSpace(parameters.StatusFilter)
                && Enum.TryParse<ContactStatus>(parameters.StatusFilter, ignoreCase: true, out var status))
            {
                query = query.Where(c => c.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(parameters.TypeFilter)
                && Enum.TryParse<ContactType>(parameters.TypeFilter, ignoreCase: true, out var type))
            {
                query = query.Where(c => c.Type == type);
            }

            if (parameters.DateFrom.HasValue || parameters.DateTo.HasValue)
            {
                var useUpdatedAt = string.Equals(parameters.DateField, "UpdatedAt", StringComparison.OrdinalIgnoreCase);
                if (useUpdatedAt)
                {
                    if (parameters.DateFrom.HasValue)
                        query = query.Where(c => c.UpdatedAt != null && c.UpdatedAt >= parameters.DateFrom!.Value);
                    if (parameters.DateTo.HasValue)
                        query = query.Where(c => c.UpdatedAt != null && c.UpdatedAt <= parameters.DateTo!.Value);
                }
                else
                {
                    if (parameters.DateFrom.HasValue)
                        query = query.Where(c => c.CreatedAt >= parameters.DateFrom!.Value);
                    if (parameters.DateTo.HasValue)
                        query = query.Where(c => c.CreatedAt <= parameters.DateTo!.Value);
                }
            }

            contacts = await query
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .ToListAsync(ct);

            customFieldsByContactId = new Dictionary<Guid, List<ContactCustomField>>();
            customFieldNames = new Dictionary<Guid, string>();
            if (parameters.CustomFieldIds is { Count: > 0 } && contacts.Count > 0)
            {
                var contactIds = contacts.Select(c => c.Id).ToList();
                var fieldDefIds = parameters.CustomFieldIds
                    .Select(CustomFieldDefinitionId.From)
                    .ToList();

                var values = await dbContext.ContactCustomFields
                    .AsNoTracking()
                    .Where(cf => contactIds.Contains(cf.ContactId) && fieldDefIds.Contains(cf.FieldDefinitionId))
                    .ToListAsync(ct);

                customFieldsByContactId = values
                    .GroupBy(v => v.ContactId.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var defs = await dbContext.CustomFieldDefinitions
                    .AsNoTracking()
                    .Where(d => fieldDefIds.Contains(d.Id))
                    .ToListAsync(ct);
                customFieldNames = defs.ToDictionary(d => d.Id.Value, d => d.FieldName);
            }
        }

        var totalRows = contacts.Count;
        if (exportJob.Status == ExportJobStatus.Queued)
        {
            exportJob.MarkProcessing(totalRows);
        }
        else
        {
            // Resume path: refresh TotalRows so the outbox event and completion
            // notification report the row count this run actually produced, not a
            // stale value frozen during a crashed prior attempt.
            exportJob.UpdateTotalRows(totalRows);
        }
        await dbContext.SaveChangesAsync(ct);

        var culture = ResolveCulture(localeContext.Locale);
        var selectedFields = parameters.Fields is { Count: > 0 }
            ? parameters.Fields
            : DefaultCoreFields;

        var (bytes, contentType, extension) = parameters.Format.ToLowerInvariant() switch
        {
            "csv" => (
                GenerateCsv(contacts, selectedFields, parameters.CustomFieldIds, customFieldsByContactId, customFieldNames, culture),
                "text/csv", "csv"),
            "xlsx" => (
                GenerateXlsx(contacts, selectedFields, parameters.CustomFieldIds, customFieldsByContactId, customFieldNames, culture),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
            "vcard" => (
                GenerateVCard(contacts),
                "text/vcard", "vcf"),
            _ => throw new NotSupportedException($"Unsupported export format: {parameters.Format}")
        };

        var opts = storageOptions.Value;
        var bucketName = $"{opts.BucketPrefix}-{tenantId}";
        var storageKey = $"{orgId}/contacts/exports/{exportJobId}.{extension}";

        await fileStorageService.UploadObjectAsync(bucketName, storageKey, bytes, contentType, ct);

        exportJob.MarkCompleted(storageKey);

        // Always emit the outbox event — the outbox+MarkCompleted SaveChangesAsync below
        // is atomic, so if a prior attempt had persisted the event, it would also have
        // persisted Completed and we wouldn't have reached this path (the early-return
        // guard above skips terminal states). Downstream consumers are inbox-guarded
        // (ADR-014) so a retry that re-emits is safely deduplicated.
        await outbox.EnqueueAsync(new ContactExportCompletedIntegrationEvent
        {
            TenantId = parameters.TenantId,
            JobId = exportJobId.Value,
            TotalRows = totalRows,
            Format = parameters.Format.ToLowerInvariant(),
            StorageKey = storageKey,
            TriggeredByUserId = parameters.TriggeredByUserId,
            CompletedAtUtc = DateTime.UtcNow
        }, ct);

        // Completion notification fires only on the Queued-origin path. On resume we do
        // not re-send because the original attempt may have already notified the user.
        // Planned idempotent follow-up (see T-010 / T-017 backlog):
        //   1. Remove this inline SendAsync call.
        //   2. Add a ContactExportCompletedNotificationHandler subscribing to
        //      ContactExportCompletedIntegrationEvent via the standard inbox table.
        //   3. Use a stable dedupe key of the form
        //        $"contacts:export-ready:{jobId}"
        //      — jobId is assigned at Queue time, survives retries unchanged, and is
        //      unique per export. The inbox primary key (MessageId, Consumer) will
        //      collapse duplicates no matter how many times the event is redelivered.
        // Until that lands, the startedFromQueued guard prevents duplicate at-most-once
        // notification on resume at the cost of possibly missing notification when a
        // crash happens between MarkProcessing and SendAsync — acceptable trade-off
        // given the user can see completion on the status page.
        if (startedFromQueued && parameters.TriggeredByUserId is { } userId)
        {
            try
            {
                await notificationService.SendAsync(new SendNotificationRequest(
                    TemplateCode: "lockey_contacts_notification_export_ready",
                    Channel: "in_app",
                    ContactId: userId,
                    RecipientAddress: userId.ToString(),
                    Variables: new Dictionary<string, string>
                    {
                        ["jobId"] = exportJobId.Value.ToString(),
                        ["format"] = parameters.Format.ToLowerInvariant(),
                        ["totalRows"] = totalRows.ToString(CultureInfo.InvariantCulture)
                    },
                    OrganizationId: orgId.ToString()), ct);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to send export-ready notification for job {ExportJobId}; export itself succeeded",
                    exportJobId);
            }
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Contact export {ExportJobId} completed: {Rows} rows -> {StorageKey} ({Format})",
            exportJobId, totalRows, storageKey, parameters.Format);
    }

    private static CultureInfo ResolveCulture(string locale)
    {
        try
        {
            return CultureInfo.GetCultureInfo(locale);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo("en-US");
        }
    }

    private static byte[] GenerateCsv(
        IReadOnlyList<Contact> contacts,
        IReadOnlyList<string> fields,
        IReadOnlyList<Guid>? customFieldIds,
        IReadOnlyDictionary<Guid, List<ContactCustomField>> customValuesByContact,
        IReadOnlyDictionary<Guid, string> customFieldNames,
        CultureInfo culture)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
        {
            // Header row
            foreach (var field in fields)
                csv.WriteField(field);

            if (customFieldIds is { Count: > 0 })
            {
                foreach (var cfId in customFieldIds)
                    csv.WriteField(customFieldNames.GetValueOrDefault(cfId, cfId.ToString()));
            }
            csv.NextRecord();

            // Data rows
            foreach (var contact in contacts)
            {
                foreach (var field in fields)
                    csv.WriteField(GetCoreFieldValue(contact, field, culture));

                if (customFieldIds is { Count: > 0 })
                {
                    customValuesByContact.TryGetValue(contact.Id.Value, out var values);
                    foreach (var cfId in customFieldIds)
                    {
                        var match = values?.FirstOrDefault(v => v.FieldDefinitionId.Value == cfId);
                        csv.WriteField(match?.Value ?? string.Empty);
                    }
                }
                csv.NextRecord();
            }
        }

        return stream.ToArray();
    }

    private static byte[] GenerateXlsx(
        IReadOnlyList<Contact> contacts,
        IReadOnlyList<string> fields,
        IReadOnlyList<Guid>? customFieldIds,
        IReadOnlyDictionary<Guid, List<ContactCustomField>> customValuesByContact,
        IReadOnlyDictionary<Guid, string> customFieldNames,
        CultureInfo culture)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Contacts");

        var allHeaders = new List<string>(fields);
        if (customFieldIds is { Count: > 0 })
        {
            foreach (var cfId in customFieldIds)
                allHeaders.Add(customFieldNames.GetValueOrDefault(cfId, cfId.ToString()));
        }

        for (var col = 0; col < allHeaders.Count; col++)
        {
            worksheet.Cell(1, col + 1).Value = allHeaders[col];
            worksheet.Cell(1, col + 1).Style.Font.Bold = true;
        }

        for (var r = 0; r < contacts.Count; r++)
        {
            var contact = contacts[r];
            var colIdx = 1;
            foreach (var field in fields)
            {
                worksheet.Cell(r + 2, colIdx++).Value = GetCoreFieldValue(contact, field, culture);
            }

            if (customFieldIds is { Count: > 0 })
            {
                customValuesByContact.TryGetValue(contact.Id.Value, out var values);
                foreach (var cfId in customFieldIds)
                {
                    var match = values?.FirstOrDefault(v => v.FieldDefinitionId.Value == cfId);
                    worksheet.Cell(r + 2, colIdx++).Value = match?.Value ?? string.Empty;
                }
            }
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] GenerateVCard(IReadOnlyList<Contact> contacts)
    {
        var sb = new StringBuilder();
        foreach (var contact in contacts)
        {
            sb.Append("BEGIN:VCARD\r\n");
            sb.Append("VERSION:3.0\r\n");
            sb.Append("FN:").Append(EscapeVCard(contact.DisplayName)).Append("\r\n");
            sb.Append("N:")
                .Append(EscapeVCard(contact.LastName ?? string.Empty)).Append(';')
                .Append(EscapeVCard(contact.FirstName ?? string.Empty))
                .Append(";;;\r\n");

            if (!string.IsNullOrWhiteSpace(contact.CompanyName))
                sb.Append("ORG:").Append(EscapeVCard(contact.CompanyName)).Append("\r\n");
            if (!string.IsNullOrWhiteSpace(contact.Email))
                sb.Append("EMAIL;TYPE=INTERNET:").Append(EscapeVCard(contact.Email)).Append("\r\n");
            if (!string.IsNullOrWhiteSpace(contact.Mobile))
                sb.Append("TEL;TYPE=CELL,VOICE:").Append(EscapeVCard(contact.Mobile)).Append("\r\n");
            if (!string.IsNullOrWhiteSpace(contact.Phone))
                sb.Append("TEL;TYPE=WORK,VOICE:").Append(EscapeVCard(contact.Phone)).Append("\r\n");
            if (!string.IsNullOrWhiteSpace(contact.Website))
                sb.Append("URL:").Append(EscapeVCard(contact.Website)).Append("\r\n");
            if (!string.IsNullOrWhiteSpace(contact.Title))
                sb.Append("TITLE:").Append(EscapeVCard(contact.Title)).Append("\r\n");

            sb.Append("END:VCARD\r\n");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EscapeVCard(string value)
    {
        // RFC 2426: escape backslash, comma, semicolon; encode newlines as literal \n.
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case ',': sb.Append("\\,"); break;
                case ';': sb.Append("\\;"); break;
                case '\r': break;
                case '\n': sb.Append("\\n"); break;
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }

    private static string GetCoreFieldValue(Contact contact, string field, CultureInfo culture) =>
        field.ToLowerInvariant() switch
        {
            "firstname" => contact.FirstName ?? string.Empty,
            "lastname" => contact.LastName ?? string.Empty,
            "displayname" => contact.DisplayName,
            "email" => contact.Email ?? string.Empty,
            "phone" => contact.Phone ?? string.Empty,
            "mobile" => contact.Mobile ?? string.Empty,
            "website" => contact.Website ?? string.Empty,
            "companyname" => contact.CompanyName ?? string.Empty,
            "taxid" => contact.TaxId ?? string.Empty,
            "title" => contact.Title ?? string.Empty,
            "type" => contact.Type.ToString(),
            "status" => contact.Status.ToString(),
            "source" => contact.Source.ToString(),
            "language" => contact.Language,
            "currency" => contact.Currency,
            "createdat" => contact.CreatedAt.ToString("G", culture),
            "updatedat" => contact.UpdatedAt?.ToString("G", culture) ?? string.Empty,
            _ => string.Empty
        };
}
