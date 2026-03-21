using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class CreateSignatureRequestTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public CreateSignatureRequestTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
    }

    private async Task<Guid> SeedDocumentAsync()
    {
        var folder = Folder.Create(_tenantId, _orgId, "TestFolder", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var document = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "contract.pdf", "application/pdf", 1024, "storage/contract.pdf");
        await _dbContext.Documents.AddAsync(document);
        await _dbContext.SaveChangesAsync();
        return document.Id.Value;
    }

    private List<SignatureRecipientInput> CreateRecipients(int count = 1)
    {
        return Enumerable.Range(1, count)
            .Select(i => new SignatureRecipientInput(Guid.NewGuid(), $"signer{i}@test.com", $"Signer {i}", i))
            .ToList();
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesSignatureRequest()
    {
        var docId = await SeedDocumentAsync();
        var handler = new CreateSignatureRequestHandler(_dbContext, _tenantAccessor, NullLogger<CreateSignatureRequestHandler>.Instance);
        var command = new CreateSignatureRequestCommand(docId, "Please sign", null, CreateRecipients());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Title.Should().Be("Please sign");
        result.Value.Status.Should().Be("Draft");
        result.Value.Recipients.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WithMultipleRecipients_AddsAllRecipients()
    {
        var docId = await SeedDocumentAsync();
        var handler = new CreateSignatureRequestHandler(_dbContext, _tenantAccessor, NullLogger<CreateSignatureRequestHandler>.Instance);
        var command = new CreateSignatureRequestCommand(docId, "Multi-sign", null, CreateRecipients(3));

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Recipients.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithExpiresAt_SetsExpiry()
    {
        var docId = await SeedDocumentAsync();
        var expiresAt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var handler = new CreateSignatureRequestHandler(_dbContext, _tenantAccessor, NullLogger<CreateSignatureRequestHandler>.Instance);
        var command = new CreateSignatureRequestCommand(docId, "Expiring", expiresAt, CreateRecipients());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public async Task Handle_InvalidDocument_ReturnsFailure()
    {
        var handler = new CreateSignatureRequestHandler(_dbContext, _tenantAccessor, NullLogger<CreateSignatureRequestHandler>.Instance);
        var command = new CreateSignatureRequestCommand(Guid.NewGuid(), "Sign this", null, CreateRecipients());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsToDatabase()
    {
        var docId = await SeedDocumentAsync();
        var handler = new CreateSignatureRequestHandler(_dbContext, _tenantAccessor, NullLogger<CreateSignatureRequestHandler>.Instance);
        var command = new CreateSignatureRequestCommand(docId, "Persist test", null, CreateRecipients(2));

        await handler.Handle(command, CancellationToken.None);

        var count = await _dbContext.SignatureRequests.CountAsync();
        count.Should().Be(1);
        var recipientCount = await _dbContext.SignatureRecipients.CountAsync();
        recipientCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ValidRequest_RecipientsPending()
    {
        var docId = await SeedDocumentAsync();
        var handler = new CreateSignatureRequestHandler(_dbContext, _tenantAccessor, NullLogger<CreateSignatureRequestHandler>.Instance);
        var command = new CreateSignatureRequestCommand(docId, "Check status", null, CreateRecipients());

        var result = await handler.Handle(command, CancellationToken.None);

        result.Value!.Recipients[0].Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_EmailWithWhitespace_NormalizesRecipientEmail()
    {
        var docId = await SeedDocumentAsync();
        var recipients = new List<SignatureRecipientInput>
        {
            new(Guid.NewGuid(), "  USER@TEST.COM  ", "User", 1)
        };
        var handler = new CreateSignatureRequestHandler(_dbContext, _tenantAccessor, NullLogger<CreateSignatureRequestHandler>.Instance);
        var command = new CreateSignatureRequestCommand(docId, "Email test", null, recipients);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Value!.Recipients[0].Email.Should().Be("user@test.com");
    }

    public void Dispose() => _dbContext.Dispose();

    private ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString(), _userId.ToString());
        return accessor;
    }
}
