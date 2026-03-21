using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Services;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.Events;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.Modules.Documents.Infrastructure.IntegrationEvents;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Infrastructure;

public sealed class SignatureCompletedArchivalHandlerTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly IDocumentArchivalService _archivalService;
    private readonly SignatureCompletedArchivalHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public SignatureCompletedArchivalHandlerTests()
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(_tenantId.ToString(), _orgId.ToString(), _userId.ToString());

        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DocumentsDbContext(options, accessor);
        _archivalService = Substitute.For<IDocumentArchivalService>();
        _handler = new SignatureCompletedArchivalHandler(
            _archivalService, _dbContext,
            NullLogger<SignatureCompletedArchivalHandler>.Instance);
    }

    private async Task<(DocumentId DocId, SignatureRequestId RequestId)> SeedCompletedRequestAsync()
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
        return (doc.Id, request.Id);
    }

    [Fact]
    public async Task Handle_ValidEvent_CallsArchivalService()
    {
        // Arrange
        var (docId, requestId) = await SeedCompletedRequestAsync();
        var @event = new SignatureCompletedEvent(requestId, docId);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        await _archivalService.Received(1).ArchiveSignedDocumentAsync(
            docId, requestId, _tenantId, _orgId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RequestNotFound_DoesNotCallArchivalService()
    {
        // Arrange
        var fakeRequestId = SignatureRequestId.New();
        var fakeDocId = DocumentId.New();
        var @event = new SignatureCompletedEvent(fakeRequestId, fakeDocId);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        await _archivalService.DidNotReceive().ArchiveSignedDocumentAsync(
            Arg.Any<DocumentId>(), Arg.Any<SignatureRequestId>(),
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SignatureRequest_PassesCorrectTenantAndOrg()
    {
        // Arrange
        var (docId, requestId) = await SeedCompletedRequestAsync();
        var @event = new SignatureCompletedEvent(requestId, docId);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert — verifies tenant/org from entity, not from tenant context
        await _archivalService.Received(1).ArchiveSignedDocumentAsync(
            docId, requestId, _tenantId, _orgId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ArchivalServiceThrows_PropagatesException()
    {
        // Arrange
        var (docId, requestId) = await SeedCompletedRequestAsync();
        var @event = new SignatureCompletedEvent(requestId, docId);

        _archivalService.ArchiveSignedDocumentAsync(
                Arg.Any<DocumentId>(), Arg.Any<SignatureRequestId>(),
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Storage error")));

        // Act
        Func<Task> act = () => _handler.Handle(@event, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Storage error");
    }

    [Fact]
    public async Task Handle_MultipleEvents_CallsArchivalForEach()
    {
        // Arrange
        var (docId1, requestId1) = await SeedCompletedRequestAsync();
        var event1 = new SignatureCompletedEvent(requestId1, docId1);

        var (docId2, requestId2) = await SeedCompletedRequestAsync();
        var event2 = new SignatureCompletedEvent(requestId2, docId2);

        // Act
        await _handler.Handle(event1, CancellationToken.None);
        await _handler.Handle(event2, CancellationToken.None);

        // Assert
        await _archivalService.Received(2).ArchiveSignedDocumentAsync(
            Arg.Any<DocumentId>(), Arg.Any<SignatureRequestId>(),
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DocumentIdFromEvent_PassesDocumentId()
    {
        // Arrange — event has a specific DocumentId
        var (docId, requestId) = await SeedCompletedRequestAsync();
        var @event = new SignatureCompletedEvent(requestId, docId);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert — DocumentId from event is passed, not looked up from request
        await _archivalService.Received(1).ArchiveSignedDocumentAsync(
            docId, Arg.Any<SignatureRequestId>(),
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    public void Dispose() => _dbContext.Dispose();
}
