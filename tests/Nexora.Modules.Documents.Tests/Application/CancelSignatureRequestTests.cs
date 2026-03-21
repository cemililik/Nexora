using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class CancelSignatureRequestTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public CancelSignatureRequestTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
    }

    private async Task<SignatureRequest> SeedRequestAsync(bool send = false)
    {
        var folder = Folder.Create(_tenantId, _orgId, "Folder", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var document = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "doc.pdf", "application/pdf", 1024, "key");
        await _dbContext.Documents.AddAsync(document);

        var request = SignatureRequest.Create(_tenantId, _orgId, document.Id, _userId, "Request");
        request.AddRecipient(Guid.NewGuid(), "signer@test.com", "Signer", 1);
        if (send) request.Send();

        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();
        return request;
    }

    [Fact]
    public async Task Handle_DraftRequest_Cancels()
    {
        var request = await SeedRequestAsync();
        var handler = new CancelSignatureRequestHandler(_dbContext, _tenantAccessor, NullLogger<CancelSignatureRequestHandler>.Instance);

        var result = await handler.Handle(new CancelSignatureRequestCommand(request.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await _dbContext.SignatureRequests.FindAsync(request.Id);
        updated!.Status.Should().Be(SignatureRequestStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_SentRequest_Cancels()
    {
        var request = await SeedRequestAsync(send: true);
        var handler = new CancelSignatureRequestHandler(_dbContext, _tenantAccessor, NullLogger<CancelSignatureRequestHandler>.Instance);

        var result = await handler.Handle(new CancelSignatureRequestCommand(request.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CompletedRequest_ReturnsFailure()
    {
        var request = await SeedRequestAsync(send: true);
        // Sign the only recipient to complete the request
        var sr = await _dbContext.SignatureRequests
            .Include(s => s.Recipients)
            .FirstAsync(s => s.Id == request.Id);
        sr.RecordSignature(sr.Recipients[0].Id, "sig", "127.0.0.1");
        await _dbContext.SaveChangesAsync();

        var handler = new CancelSignatureRequestHandler(_dbContext, _tenantAccessor, NullLogger<CancelSignatureRequestHandler>.Instance);

        var result = await handler.Handle(new CancelSignatureRequestCommand(request.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonExistentRequest_ReturnsFailure()
    {
        var handler = new CancelSignatureRequestHandler(_dbContext, _tenantAccessor, NullLogger<CancelSignatureRequestHandler>.Instance);

        var result = await handler.Handle(new CancelSignatureRequestCommand(Guid.NewGuid()), CancellationToken.None);

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
