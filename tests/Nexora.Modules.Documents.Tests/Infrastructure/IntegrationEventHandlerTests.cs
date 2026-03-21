using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.Events;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.Modules.Documents.Infrastructure.IntegrationEvents;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Infrastructure;

public sealed class IntegrationEventHandlerTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public IntegrationEventHandlerTests()
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(_tenantId.ToString(), _orgId.ToString(), _userId.ToString());

        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DocumentsDbContext(options, accessor);
        _eventBus = Substitute.For<IEventBus>();
    }

    private async Task<(Document Doc, SignatureRequest Request)> SeedSignatureRequestAsync()
    {
        var folder = Folder.Create(_tenantId, _orgId, "Test", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var doc = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "contract.pdf", "application/pdf", 2048, "key/contract.pdf");
        await _dbContext.Documents.AddAsync(doc);

        var request = SignatureRequest.Create(_tenantId, _orgId, doc.Id, _userId, "Sign Contract");
        request.AddRecipient(Guid.NewGuid(), "signer@test.com", "Signer", 1);
        request.Send();
        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        return (doc, request);
    }

    [Fact]
    public async Task SignatureCompletedHandler_ValidEvent_PublishesIntegrationEvent()
    {
        // Arrange
        var (doc, request) = await SeedSignatureRequestAsync();
        var handler = new SignatureCompletedDomainEventHandler(
            _eventBus, _dbContext,
            NullLogger<SignatureCompletedDomainEventHandler>.Instance);
        var @event = new SignatureCompletedEvent(request.Id, doc.Id);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        await _eventBus.Received(1).PublishAsync(
            Arg.Is<SignatureCompletedIntegrationEvent>(e =>
                e.SignatureRequestId == request.Id.Value &&
                e.DocumentId == doc.Id.Value &&
                e.TenantId == _tenantId.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignatureCompletedHandler_RequestNotFound_DoesNotPublish()
    {
        // Arrange
        var handler = new SignatureCompletedDomainEventHandler(
            _eventBus, _dbContext,
            NullLogger<SignatureCompletedDomainEventHandler>.Instance);
        var @event = new SignatureCompletedEvent(SignatureRequestId.New(), DocumentId.New());

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        await _eventBus.DidNotReceive().PublishAsync(
            Arg.Any<SignatureCompletedIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignatureCompletedHandler_SignatureRequest_ReadsTenantFromEntity()
    {
        // Arrange
        var (doc, request) = await SeedSignatureRequestAsync();
        var handler = new SignatureCompletedDomainEventHandler(
            _eventBus, _dbContext,
            NullLogger<SignatureCompletedDomainEventHandler>.Instance);
        var @event = new SignatureCompletedEvent(request.Id, doc.Id);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert — TenantId should come from entity, matching our seeded tenantId
        await _eventBus.Received(1).PublishAsync(
            Arg.Is<SignatureCompletedIntegrationEvent>(e => e.TenantId == _tenantId.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DocumentSignedHandler_ValidEvent_PublishesIntegrationEvent()
    {
        // Arrange
        var (doc, request) = await SeedSignatureRequestAsync();
        var recipient = request.Recipients[0];
        var handler = new DocumentSignedDomainEventHandler(
            _eventBus, _dbContext,
            NullLogger<DocumentSignedDomainEventHandler>.Instance);
        var @event = new DocumentSignedEvent(request.Id, recipient.Id);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        await _eventBus.Received(1).PublishAsync(
            Arg.Is<DocumentSignedIntegrationEvent>(e =>
                e.SignatureRequestId == request.Id.Value &&
                e.DocumentId == doc.Id.Value &&
                e.RecipientContactId == recipient.ContactId &&
                e.TenantId == _tenantId.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DocumentSignedHandler_RequestNotFound_DoesNotPublish()
    {
        var handler = new DocumentSignedDomainEventHandler(
            _eventBus, _dbContext,
            NullLogger<DocumentSignedDomainEventHandler>.Instance);
        var @event = new DocumentSignedEvent(SignatureRequestId.New(), SignatureRecipientId.New());

        await handler.Handle(@event, CancellationToken.None);

        await _eventBus.DidNotReceive().PublishAsync(
            Arg.Any<DocumentSignedIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DocumentSignedHandler_RecipientFromDifferentRequest_DoesNotPublish()
    {
        // Arrange — create two separate requests, try to use recipient from one with the other's ID
        var (_, request1) = await SeedSignatureRequestAsync();
        var recipient1 = request1.Recipients[0];

        // Create second request with its own recipient
        var folder = Folder.Create(_tenantId, _orgId, "Folder2", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var doc2 = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "doc2.pdf", "application/pdf", 1024, "key/doc2.pdf");
        await _dbContext.Documents.AddAsync(doc2);
        var request2 = SignatureRequest.Create(_tenantId, _orgId, doc2.Id, _userId, "Sign Doc 2");
        request2.AddRecipient(Guid.NewGuid(), "other@test.com", "Other", 1);
        request2.Send();
        await _dbContext.SignatureRequests.AddAsync(request2);
        await _dbContext.SaveChangesAsync();

        var handler = new DocumentSignedDomainEventHandler(
            _eventBus, _dbContext,
            NullLogger<DocumentSignedDomainEventHandler>.Instance);

        // Use request2's ID but recipient1's ID — compound lookup should fail
        var @event = new DocumentSignedEvent(request2.Id, recipient1.Id);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert — should not publish because recipient doesn't belong to request2
        await _eventBus.DidNotReceive().PublishAsync(
            Arg.Any<DocumentSignedIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DocumentArchivedHandler_ValidEvent_PublishesIntegrationEvent()
    {
        // Arrange
        var folder = Folder.Create(_tenantId, _orgId, "ArchiveFolder", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var doc = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "archived.pdf", "application/pdf", 512, "key/archived.pdf");
        await _dbContext.Documents.AddAsync(doc);
        await _dbContext.SaveChangesAsync();

        var handler = new DocumentArchivedDomainEventHandler(
            _eventBus, _dbContext,
            NullLogger<DocumentArchivedDomainEventHandler>.Instance);
        var @event = new DocumentArchivedEvent(doc.Id);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        await _eventBus.Received(1).PublishAsync(
            Arg.Is<DocumentArchivedIntegrationEvent>(e =>
                e.DocumentId == doc.Id.Value &&
                e.TenantId == _tenantId.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DocumentArchivedHandler_DocumentNotFound_DoesNotPublish()
    {
        var handler = new DocumentArchivedDomainEventHandler(
            _eventBus, _dbContext,
            NullLogger<DocumentArchivedDomainEventHandler>.Instance);
        var @event = new DocumentArchivedEvent(DocumentId.New());

        await handler.Handle(@event, CancellationToken.None);

        await _eventBus.DidNotReceive().PublishAsync(
            Arg.Any<DocumentArchivedIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    public void Dispose() => _dbContext.Dispose();
}
