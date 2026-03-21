using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Queries;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class GetSignatureRequestsTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GetSignatureRequestsTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
    }

    private async Task<DocumentId> SeedDocumentAsync()
    {
        var folder = Folder.Create(_tenantId, _orgId, "Folder", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var document = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "doc.pdf", "application/pdf", 1024, "key");
        await _dbContext.Documents.AddAsync(document);
        await _dbContext.SaveChangesAsync();
        return document.Id;
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsEmpty()
    {
        var handler = new GetSignatureRequestsHandler(_dbContext, _tenantAccessor, NullLogger<GetSignatureRequestsHandler>.Instance);

        var result = await handler.Handle(new GetSignatureRequestsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithRequests_ReturnsAll()
    {
        var docId = await SeedDocumentAsync();
        var req1 = SignatureRequest.Create(_tenantId, _orgId, docId, _userId, "Request 1");
        req1.AddRecipient(Guid.NewGuid(), "a@test.com", "A", 1);
        var req2 = SignatureRequest.Create(_tenantId, _orgId, docId, _userId, "Request 2");
        req2.AddRecipient(Guid.NewGuid(), "b@test.com", "B", 1);
        await _dbContext.SignatureRequests.AddRangeAsync(req1, req2);
        await _dbContext.SaveChangesAsync();

        var handler = new GetSignatureRequestsHandler(_dbContext, _tenantAccessor, NullLogger<GetSignatureRequestsHandler>.Instance);

        var result = await handler.Handle(new GetSignatureRequestsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithStatusFilter_FiltersByStatus()
    {
        var docId = await SeedDocumentAsync();
        var draft = SignatureRequest.Create(_tenantId, _orgId, docId, _userId, "Draft");
        draft.AddRecipient(Guid.NewGuid(), "a@test.com", "A", 1);
        var sent = SignatureRequest.Create(_tenantId, _orgId, docId, _userId, "Sent");
        sent.AddRecipient(Guid.NewGuid(), "b@test.com", "B", 1);
        sent.Send();
        await _dbContext.SignatureRequests.AddRangeAsync(draft, sent);
        await _dbContext.SaveChangesAsync();

        var handler = new GetSignatureRequestsHandler(_dbContext, _tenantAccessor, NullLogger<GetSignatureRequestsHandler>.Instance);

        var result = await handler.Handle(new GetSignatureRequestsQuery(Status: "Sent"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items[0].Title.Should().Be("Sent");
    }

    [Fact]
    public async Task Handle_WithDocumentFilter_FiltersByDocument()
    {
        var docId1 = await SeedDocumentAsync();
        var docId2 = await SeedDocumentAsync();
        var req1 = SignatureRequest.Create(_tenantId, _orgId, docId1, _userId, "For Doc 1");
        req1.AddRecipient(Guid.NewGuid(), "a@test.com", "A", 1);
        var req2 = SignatureRequest.Create(_tenantId, _orgId, docId2, _userId, "For Doc 2");
        req2.AddRecipient(Guid.NewGuid(), "b@test.com", "B", 1);
        await _dbContext.SignatureRequests.AddRangeAsync(req1, req2);
        await _dbContext.SaveChangesAsync();

        var handler = new GetSignatureRequestsHandler(_dbContext, _tenantAccessor, NullLogger<GetSignatureRequestsHandler>.Instance);

        var result = await handler.Handle(new GetSignatureRequestsQuery(DocumentId: docId1.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items[0].Title.Should().Be("For Doc 1");
    }

    public void Dispose() => _dbContext.Dispose();

    private ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString(), _userId.ToString());
        return accessor;
    }
}
