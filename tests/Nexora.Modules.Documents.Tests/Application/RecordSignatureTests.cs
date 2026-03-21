using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class RecordSignatureTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public RecordSignatureTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
    }

    private async Task<(SignatureRequest Request, SignatureRecipientId RecipientId)> SeedSentRequestAsync(int recipientCount = 1)
    {
        var folder = Folder.Create(_tenantId, _orgId, "Folder", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var document = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "doc.pdf", "application/pdf", 1024, "key");
        await _dbContext.Documents.AddAsync(document);

        var request = SignatureRequest.Create(_tenantId, _orgId, document.Id, _userId, "Sign this");
        for (var i = 1; i <= recipientCount; i++)
            request.AddRecipient(Guid.NewGuid(), $"signer{i}@test.com", $"Signer {i}", i);
        request.Send();

        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        return (request, request.Recipients[0].Id);
    }

    [Fact]
    public async Task Handle_ValidSignature_Succeeds()
    {
        var (request, recipientId) = await SeedSentRequestAsync();
        var handler = new RecordSignatureHandler(_dbContext, _tenantAccessor, NullLogger<RecordSignatureHandler>.Instance);
        var command = new RecordSignatureCommand(request.Id.Value, recipientId.Value, "sig-data", "127.0.0.1");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidSignature_ChangesRecipientStatus()
    {
        var (request, recipientId) = await SeedSentRequestAsync();
        var handler = new RecordSignatureHandler(_dbContext, _tenantAccessor, NullLogger<RecordSignatureHandler>.Instance);
        var command = new RecordSignatureCommand(request.Id.Value, recipientId.Value, "sig-data", "127.0.0.1");

        await handler.Handle(command, CancellationToken.None);

        var updated = await _dbContext.SignatureRecipients.FindAsync(recipientId);
        updated!.Status.Should().Be(SignatureRecipientStatus.Signed);
        updated.SignedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_AllRecipientsSigned_CompletesRequest()
    {
        var (request, recipientId) = await SeedSentRequestAsync(1);
        var handler = new RecordSignatureHandler(_dbContext, _tenantAccessor, NullLogger<RecordSignatureHandler>.Instance);
        var command = new RecordSignatureCommand(request.Id.Value, recipientId.Value, "sig-data", "127.0.0.1");

        await handler.Handle(command, CancellationToken.None);

        var updated = await _dbContext.SignatureRequests
            .Include(s => s.Recipients)
            .FirstAsync(s => s.Id == request.Id);
        updated.Status.Should().Be(SignatureRequestStatus.Completed);
    }

    [Fact]
    public async Task Handle_PartialSign_SetsPartiallySignedStatus()
    {
        var (request, recipientId) = await SeedSentRequestAsync(2);
        var handler = new RecordSignatureHandler(_dbContext, _tenantAccessor, NullLogger<RecordSignatureHandler>.Instance);
        var command = new RecordSignatureCommand(request.Id.Value, recipientId.Value, "sig-data", "127.0.0.1");

        await handler.Handle(command, CancellationToken.None);

        var updated = await _dbContext.SignatureRequests
            .Include(s => s.Recipients)
            .FirstAsync(s => s.Id == request.Id);
        updated.Status.Should().Be(SignatureRequestStatus.PartiallySigned);
    }

    [Fact]
    public async Task Handle_NonExistentRequest_ReturnsFailure()
    {
        var handler = new RecordSignatureHandler(_dbContext, _tenantAccessor, NullLogger<RecordSignatureHandler>.Instance);
        var command = new RecordSignatureCommand(Guid.NewGuid(), Guid.NewGuid(), "sig", "127.0.0.1");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonExistentRecipient_ReturnsFailure()
    {
        var (request, _) = await SeedSentRequestAsync();
        var handler = new RecordSignatureHandler(_dbContext, _tenantAccessor, NullLogger<RecordSignatureHandler>.Instance);
        var command = new RecordSignatureCommand(request.Id.Value, Guid.NewGuid(), "sig", "127.0.0.1");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_AlreadySigned_ReturnsFailure()
    {
        var (request, recipientId) = await SeedSentRequestAsync();
        var handler = new RecordSignatureHandler(_dbContext, _tenantAccessor, NullLogger<RecordSignatureHandler>.Instance);
        await handler.Handle(
            new RecordSignatureCommand(request.Id.Value, recipientId.Value, "sig1", "127.0.0.1"),
            CancellationToken.None);

        var result = await handler.Handle(
            new RecordSignatureCommand(request.Id.Value, recipientId.Value, "sig2", "127.0.0.1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DraftRequest_ReturnsFailure()
    {
        var folder = Folder.Create(_tenantId, _orgId, "Folder", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var document = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "doc.pdf", "application/pdf", 1024, "key");
        await _dbContext.Documents.AddAsync(document);
        var request = SignatureRequest.Create(_tenantId, _orgId, document.Id, _userId, "Draft");
        request.AddRecipient(Guid.NewGuid(), "signer@test.com", "Signer", 1);
        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        var handler = new RecordSignatureHandler(_dbContext, _tenantAccessor, NullLogger<RecordSignatureHandler>.Instance);
        var command = new RecordSignatureCommand(request.Id.Value, request.Recipients[0].Id.Value, "sig", "127.0.0.1");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    public void Dispose() => _dbContext.Dispose();

    private ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString(), _userId.ToString());
        return accessor;
    }
}
