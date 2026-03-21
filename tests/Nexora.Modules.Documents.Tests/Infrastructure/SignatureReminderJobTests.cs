using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.Modules.Documents.Infrastructure.Jobs;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Infrastructure;

public sealed class SignatureReminderJobTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly INotificationService _notificationService;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public SignatureReminderJobTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        ((TenantContextAccessor)_tenantAccessor).SetTenant(
            _tenantId.ToString(), _orgId.ToString(), _userId.ToString());

        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
        _notificationService = Substitute.For<INotificationService>();
        _notificationService.SendAsync(Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());
    }

    private async Task<DocumentId> SeedDocumentAsync()
    {
        var folder = Folder.Create(_tenantId, _orgId, "Test", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var doc = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "test.pdf", "application/pdf", 1024, "key/test.pdf");
        await _dbContext.Documents.AddAsync(doc);
        await _dbContext.SaveChangesAsync();
        return doc.Id;
    }

    [Fact]
    public async Task Execute_WithPendingRecipient_SendsReminder()
    {
        // Arrange
        var docId = await SeedDocumentAsync();
        var request = SignatureRequest.Create(_tenantId, _orgId, docId, _userId, "Contract");
        request.AddRecipient(Guid.NewGuid(), "alice@test.com", "Alice", 1);
        request.Send();
        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        var job = new SignatureReminderJob(
            _tenantAccessor, _dbContext, _notificationService, NullLogger<SignatureReminderJob>.Instance);
        var parameters = new SignatureReminderJobParams { TenantId = _tenantId.ToString() };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(r =>
                r.RecipientAddress == "alice@test.com" &&
                r.TemplateCode == "lockey_documents_notification_signature_reminder"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithMultiplePendingRecipients_SendsMultipleReminders()
    {
        // Arrange
        var docId = await SeedDocumentAsync();
        var request = SignatureRequest.Create(_tenantId, _orgId, docId, _userId, "Contract");
        request.AddRecipient(Guid.NewGuid(), "alice@test.com", "Alice", 1);
        request.AddRecipient(Guid.NewGuid(), "bob@test.com", "Bob", 2);
        request.Send();
        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        var job = new SignatureReminderJob(
            _tenantAccessor, _dbContext, _notificationService, NullLogger<SignatureReminderJob>.Instance);
        var parameters = new SignatureReminderJobParams { TenantId = _tenantId.ToString() };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        await _notificationService.Received(2).SendAsync(
            Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithNoPendingRecipients_DoesNotSendAny()
    {
        // Arrange — no requests at all
        var job = new SignatureReminderJob(
            _tenantAccessor, _dbContext, _notificationService, NullLogger<SignatureReminderJob>.Instance);
        var parameters = new SignatureReminderJobParams { TenantId = _tenantId.ToString() };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        await _notificationService.DidNotReceive().SendAsync(
            Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_DraftRequest_DoesNotSendReminder()
    {
        // Arrange
        var docId = await SeedDocumentAsync();
        var request = SignatureRequest.Create(_tenantId, _orgId, docId, _userId, "Draft Contract");
        request.AddRecipient(Guid.NewGuid(), "alice@test.com", "Alice", 1);
        // Not sent — still in Draft
        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        var job = new SignatureReminderJob(
            _tenantAccessor, _dbContext, _notificationService, NullLogger<SignatureReminderJob>.Instance);
        var parameters = new SignatureReminderJobParams { TenantId = _tenantId.ToString() };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        await _notificationService.DidNotReceive().SendAsync(
            Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_DifferentTenant_DoesNotSendReminder()
    {
        // Arrange
        var docId = await SeedDocumentAsync();
        var otherTenantId = Guid.NewGuid();
        var request = SignatureRequest.Create(otherTenantId, _orgId, docId, _userId, "Other Tenant");
        request.AddRecipient(Guid.NewGuid(), "alice@test.com", "Alice", 1);
        request.Send();
        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        var job = new SignatureReminderJob(
            _tenantAccessor, _dbContext, _notificationService, NullLogger<SignatureReminderJob>.Instance);
        var parameters = new SignatureReminderJobParams { TenantId = _tenantId.ToString() };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        await _notificationService.DidNotReceive().SendAsync(
            Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ReminderVariables_ContainsRequestTitleAndRecipientName()
    {
        // Arrange
        var docId = await SeedDocumentAsync();
        var request = SignatureRequest.Create(_tenantId, _orgId, docId, _userId, "NDA Agreement");
        request.AddRecipient(Guid.NewGuid(), "alice@test.com", "Alice Smith", 1);
        request.Send();
        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        var job = new SignatureReminderJob(
            _tenantAccessor, _dbContext, _notificationService, NullLogger<SignatureReminderJob>.Instance);
        var parameters = new SignatureReminderJobParams { TenantId = _tenantId.ToString() };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(r =>
                r.Variables["requestTitle"] == "NDA Agreement" &&
                r.Variables["recipientName"] == "Alice Smith"),
            Arg.Any<CancellationToken>());
    }

    public void Dispose() => _dbContext.Dispose();
}
