using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class SendSignatureRequestTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public SendSignatureRequestTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
    }

    private async Task<SignatureRequest> SeedDraftRequestAsync(int recipientCount = 1)
    {
        var folder = Folder.Create(_tenantId, _orgId, "Folder", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var document = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "doc.pdf", "application/pdf", 1024, "key");
        await _dbContext.Documents.AddAsync(document);

        var request = SignatureRequest.Create(_tenantId, _orgId, document.Id, _userId, "Sign this");
        for (var i = 1; i <= recipientCount; i++)
            request.AddRecipient(Guid.NewGuid(), $"signer{i}@test.com", $"Signer {i}", i);

        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();
        return request;
    }

    [Fact]
    public async Task Handle_DraftWithRecipients_Sends()
    {
        var request = await SeedDraftRequestAsync();
        var handler = new SendSignatureRequestHandler(_dbContext, _tenantAccessor, NullLogger<SendSignatureRequestHandler>.Instance);

        var result = await handler.Handle(new SendSignatureRequestCommand(request.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DraftWithRecipients_ChangesStatusToSent()
    {
        var request = await SeedDraftRequestAsync();
        var handler = new SendSignatureRequestHandler(_dbContext, _tenantAccessor, NullLogger<SendSignatureRequestHandler>.Instance);

        await handler.Handle(new SendSignatureRequestCommand(request.Id.Value), CancellationToken.None);

        var updated = await _dbContext.SignatureRequests.FindAsync(request.Id);
        updated!.Status.Should().Be(SignatureRequestStatus.Sent);
    }

    [Fact]
    public async Task Handle_AlreadySent_ReturnsFailure()
    {
        var request = await SeedDraftRequestAsync();
        request.Send();
        await _dbContext.SaveChangesAsync();

        var handler = new SendSignatureRequestHandler(_dbContext, _tenantAccessor, NullLogger<SendSignatureRequestHandler>.Instance);

        var result = await handler.Handle(new SendSignatureRequestCommand(request.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonExistentRequest_ReturnsFailure()
    {
        var handler = new SendSignatureRequestHandler(_dbContext, _tenantAccessor, NullLogger<SendSignatureRequestHandler>.Instance);

        var result = await handler.Handle(new SendSignatureRequestCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DraftNoRecipients_ReturnsFailure()
    {
        var folder = Folder.Create(_tenantId, _orgId, "Folder", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var document = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "doc.pdf", "application/pdf", 1024, "key");
        await _dbContext.Documents.AddAsync(document);
        var request = SignatureRequest.Create(_tenantId, _orgId, document.Id, _userId, "No recipients");
        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        var handler = new SendSignatureRequestHandler(_dbContext, _tenantAccessor, NullLogger<SendSignatureRequestHandler>.Instance);

        var result = await handler.Handle(new SendSignatureRequestCommand(request.Id.Value), CancellationToken.None);

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
