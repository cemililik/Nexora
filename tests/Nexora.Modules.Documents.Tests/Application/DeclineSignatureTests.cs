using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class DeclineSignatureTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public DeclineSignatureTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
    }

    private async Task<(SignatureRequest Request, SignatureRecipientId RecipientId)> SeedSentRequestAsync()
    {
        var folder = Folder.Create(_tenantId, _orgId, "Folder", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var document = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "doc.pdf", "application/pdf", 1024, "key");
        await _dbContext.Documents.AddAsync(document);

        var request = SignatureRequest.Create(_tenantId, _orgId, document.Id, _userId, "Sign this");
        request.AddRecipient(Guid.NewGuid(), "signer@test.com", "Signer", 1);
        request.Send();

        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        return (request, request.Recipients[0].Id);
    }

    [Fact]
    public async Task Handle_PendingRecipient_Declines()
    {
        var (request, recipientId) = await SeedSentRequestAsync();
        var handler = new DeclineSignatureHandler(_dbContext, _tenantAccessor, NullLogger<DeclineSignatureHandler>.Instance);

        var result = await handler.Handle(
            new DeclineSignatureCommand(request.Id.Value, recipientId.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await _dbContext.SignatureRecipients.FindAsync(recipientId);
        updated!.Status.Should().Be(SignatureRecipientStatus.Declined);
    }

    [Fact]
    public async Task Handle_AlreadySigned_ReturnsFailure()
    {
        var (request, recipientId) = await SeedSentRequestAsync();
        var recipient = await _dbContext.SignatureRecipients.FindAsync(recipientId);
        recipient!.Sign("sig-data", "127.0.0.1");
        await _dbContext.SaveChangesAsync();

        var handler = new DeclineSignatureHandler(_dbContext, _tenantAccessor, NullLogger<DeclineSignatureHandler>.Instance);

        var result = await handler.Handle(
            new DeclineSignatureCommand(request.Id.Value, recipientId.Value), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonExistentRecipient_ReturnsFailure()
    {
        var (request, _) = await SeedSentRequestAsync();
        var handler = new DeclineSignatureHandler(_dbContext, _tenantAccessor, NullLogger<DeclineSignatureHandler>.Instance);

        var result = await handler.Handle(
            new DeclineSignatureCommand(request.Id.Value, Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonExistentRequest_ReturnsFailure()
    {
        var handler = new DeclineSignatureHandler(_dbContext, _tenantAccessor, NullLogger<DeclineSignatureHandler>.Instance);

        var result = await handler.Handle(
            new DeclineSignatureCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

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
