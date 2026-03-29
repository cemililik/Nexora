using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    IActiveTenantProvider tenantProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<SignatureReminderJob> logger) : PlatformJob<SignatureReminderJobParams>(tenantProvider, scopeFactory, logger)
{
    protected override string? GetRequiredModule() => "documents";

    /// <inheritdoc />
    protected override async Task ExecuteForTenantAsync(
        SignatureReminderJobParams parameters, ActiveTenantInfo tenant,
        IServiceProvider scopedServices, CancellationToken ct)
    {
        var dbContext = scopedServices.GetRequiredService<DocumentsDbContext>();
        var notificationService = scopedServices.GetRequiredService<INotificationService>();

        var activeRequests = await dbContext.SignatureRequests
            .Include(s => s.Recipients)
            .Where(s => s.Status == SignatureRequestStatus.Sent || s.Status == SignatureRequestStatus.PartiallySigned)
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

        logger.LogInformation("Sent {Count} signature reminders", pendingRecipients.Count);
    }
}
