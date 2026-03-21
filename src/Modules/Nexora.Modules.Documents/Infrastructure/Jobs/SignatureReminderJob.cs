using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Infrastructure.Jobs;

/// <summary>Parameters for the signature reminder job.</summary>
public sealed record SignatureReminderJobParams : JobParams;

/// <summary>
/// Recurring job that sends reminders to unsigned recipients on active signature requests.
/// Runs daily. Only targets recipients in Pending or Viewed status.
/// </summary>
public sealed class SignatureReminderJob(
    ITenantContextAccessor tenantContextAccessor,
    DocumentsDbContext dbContext,
    INotificationService notificationService,
    ILogger<SignatureReminderJob> logger) : NexoraJob<SignatureReminderJobParams>(tenantContextAccessor, logger)
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(SignatureReminderJobParams parameters, CancellationToken ct)
    {
        var tenantId = Guid.Parse(parameters.TenantId);

        var activeRequests = await dbContext.SignatureRequests
            .Include(s => s.Recipients)
            .Where(s => s.TenantId == tenantId
                && (s.Status == SignatureRequestStatus.Sent || s.Status == SignatureRequestStatus.PartiallySigned))
            .ToListAsync(ct);

        var pendingRecipients = activeRequests
            .SelectMany(s => s.Recipients
                .Where(r => r.Status is SignatureRecipientStatus.Pending or SignatureRecipientStatus.Viewed)
                .Select(r => new { Recipient = r, Request = s }))
            .ToList();

        if (pendingRecipients.Count == 0)
            return;

        foreach (var item in pendingRecipients)
        {
            await notificationService.SendAsync(new SendNotificationRequest(
                TemplateCode: "lockey_documents_notification_signature_reminder",
                Channel: "Email",
                ContactId: item.Recipient.ContactId,
                RecipientAddress: item.Recipient.Email,
                Variables: new Dictionary<string, string>
                {
                    { "requestTitle", item.Request.Title },
                    { "recipientName", item.Recipient.Name }
                }
            ), ct);
        }

        logger.LogInformation(
            "Sent {Count} signature reminders in tenant {TenantId}",
            pendingRecipients.Count, tenantId);
    }
}
