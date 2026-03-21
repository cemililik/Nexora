using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Queries;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class GetSignatureRequestByIdTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GetSignatureRequestByIdTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
    }

    private async Task<SignatureRequest> SeedRequestWithRecipientsAsync()
    {
        var folder = Folder.Create(_tenantId, _orgId, "Folder", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var document = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "doc.pdf", "application/pdf", 1024, "key");
        await _dbContext.Documents.AddAsync(document);

        var request = SignatureRequest.Create(_tenantId, _orgId, document.Id, _userId, "Test Request");
        request.AddRecipient(Guid.NewGuid(), "signer1@test.com", "Signer 1", 1);
        request.AddRecipient(Guid.NewGuid(), "signer2@test.com", "Signer 2", 2);

        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();
        return request;
    }

    [Fact]
    public async Task Handle_ExistingRequest_ReturnsDetail()
    {
        var request = await SeedRequestWithRecipientsAsync();
        var handler = new GetSignatureRequestByIdHandler(_dbContext, _tenantAccessor, NullLogger<GetSignatureRequestByIdHandler>.Instance);

        var result = await handler.Handle(new GetSignatureRequestByIdQuery(request.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Title.Should().Be("Test Request");
        result.Value.Status.Should().Be("Draft");
        result.Value.Recipients.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ExistingRequest_OrdersRecipientsBySigningOrder()
    {
        var request = await SeedRequestWithRecipientsAsync();
        var handler = new GetSignatureRequestByIdHandler(_dbContext, _tenantAccessor, NullLogger<GetSignatureRequestByIdHandler>.Instance);

        var result = await handler.Handle(new GetSignatureRequestByIdQuery(request.Id.Value), CancellationToken.None);

        result.Value!.Recipients[0].SigningOrder.Should().Be(1);
        result.Value.Recipients[1].SigningOrder.Should().Be(2);
    }

    [Fact]
    public async Task Handle_NonExistentRequest_ReturnsFailure()
    {
        var handler = new GetSignatureRequestByIdHandler(_dbContext, _tenantAccessor, NullLogger<GetSignatureRequestByIdHandler>.Instance);

        var result = await handler.Handle(new GetSignatureRequestByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DifferentTenant_ReturnsFailure()
    {
        var folder = Folder.Create(Guid.NewGuid(), Guid.NewGuid(), "Other", Guid.NewGuid());
        await _dbContext.Folders.AddAsync(folder);
        var document = Document.Create(Guid.NewGuid(), Guid.NewGuid(), folder.Id, Guid.NewGuid(),
            "other.pdf", "application/pdf", 100, "key");
        await _dbContext.Documents.AddAsync(document);
        var otherRequest = SignatureRequest.Create(Guid.NewGuid(), Guid.NewGuid(), document.Id, Guid.NewGuid(), "Other");
        await _dbContext.SignatureRequests.AddAsync(otherRequest);
        await _dbContext.SaveChangesAsync();

        var handler = new GetSignatureRequestByIdHandler(_dbContext, _tenantAccessor, NullLogger<GetSignatureRequestByIdHandler>.Instance);

        var result = await handler.Handle(new GetSignatureRequestByIdQuery(otherRequest.Id.Value), CancellationToken.None);

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
