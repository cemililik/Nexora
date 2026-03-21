using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.Modules.Documents.Infrastructure.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Infrastructure;

public sealed class SignatureExpiryJobTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public SignatureExpiryJobTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        ((TenantContextAccessor)_tenantAccessor).SetTenant(
            _tenantId.ToString(), _orgId.ToString(), _userId.ToString());

        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
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
    public async Task Execute_WithExpiredRequest_MarksAsExpired()
    {
        // Arrange
        var docId = await SeedDocumentAsync();
        var request = SignatureRequest.Create(_tenantId, _orgId, docId, _userId, "Expired Contract",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
        request.AddRecipient(Guid.NewGuid(), "a@test.com", "Alice", 1);
        request.Send();
        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        var job = new SignatureExpiryJob(_tenantAccessor, _dbContext, NullLogger<SignatureExpiryJob>.Instance);
        var parameters = new SignatureExpiryJobParams { TenantId = _tenantId.ToString() };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var updated = await _dbContext.SignatureRequests
            .Include(s => s.Recipients)
            .FirstAsync(s => s.Id == request.Id);
        updated.Status.Should().Be(SignatureRequestStatus.Expired);
        updated.Recipients.Should().AllSatisfy(r => r.Status.Should().Be(SignatureRecipientStatus.Expired));
    }

    [Fact]
    public async Task Execute_WithNonExpiredRequest_DoesNotChange()
    {
        // Arrange
        var docId = await SeedDocumentAsync();
        var request = SignatureRequest.Create(_tenantId, _orgId, docId, _userId, "Future Contract",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        request.AddRecipient(Guid.NewGuid(), "a@test.com", "Alice", 1);
        request.Send();
        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        var job = new SignatureExpiryJob(_tenantAccessor, _dbContext, NullLogger<SignatureExpiryJob>.Instance);
        var parameters = new SignatureExpiryJobParams { TenantId = _tenantId.ToString() };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var updated = await _dbContext.SignatureRequests.FirstAsync(s => s.Id == request.Id);
        updated.Status.Should().Be(SignatureRequestStatus.Sent);
    }

    [Fact]
    public async Task Execute_AlreadyExpiredRequest_Skips()
    {
        // Arrange
        var docId = await SeedDocumentAsync();
        var request = SignatureRequest.Create(_tenantId, _orgId, docId, _userId, "Already Expired",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)));
        request.AddRecipient(Guid.NewGuid(), "a@test.com", "Alice", 1);
        request.Send();
        request.Expire();
        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        var job = new SignatureExpiryJob(_tenantAccessor, _dbContext, NullLogger<SignatureExpiryJob>.Instance);
        var parameters = new SignatureExpiryJobParams { TenantId = _tenantId.ToString() };

        // Act & Assert — should complete without error
        await job.RunAsync(parameters, CancellationToken.None);
    }

    [Fact]
    public async Task Execute_WithNoExpiryDate_Skips()
    {
        // Arrange
        var docId = await SeedDocumentAsync();
        var request = SignatureRequest.Create(_tenantId, _orgId, docId, _userId, "No Expiry");
        request.AddRecipient(Guid.NewGuid(), "a@test.com", "Alice", 1);
        request.Send();
        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        var job = new SignatureExpiryJob(_tenantAccessor, _dbContext, NullLogger<SignatureExpiryJob>.Instance);
        var parameters = new SignatureExpiryJobParams { TenantId = _tenantId.ToString() };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var updated = await _dbContext.SignatureRequests.FirstAsync(s => s.Id == request.Id);
        updated.Status.Should().Be(SignatureRequestStatus.Sent);
    }

    [Fact]
    public async Task Execute_DifferentTenant_DoesNotExpire()
    {
        // Arrange
        var docId = await SeedDocumentAsync();
        var otherTenantId = Guid.NewGuid();
        var request = SignatureRequest.Create(otherTenantId, _orgId, docId, _userId, "Other Tenant",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
        request.AddRecipient(Guid.NewGuid(), "a@test.com", "Alice", 1);
        request.Send();
        await _dbContext.SignatureRequests.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        var job = new SignatureExpiryJob(_tenantAccessor, _dbContext, NullLogger<SignatureExpiryJob>.Instance);
        var parameters = new SignatureExpiryJobParams { TenantId = _tenantId.ToString() };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var updated = await _dbContext.SignatureRequests.FirstAsync(s => s.Id == request.Id);
        updated.Status.Should().Be(SignatureRequestStatus.Sent);
    }

    [Fact]
    public async Task Execute_EmptyDatabase_CompletesWithoutError()
    {
        var job = new SignatureExpiryJob(_tenantAccessor, _dbContext, NullLogger<SignatureExpiryJob>.Instance);
        var parameters = new SignatureExpiryJobParams { TenantId = _tenantId.ToString() };

        // Act & Assert
        await job.RunAsync(parameters, CancellationToken.None);
    }

    public void Dispose() => _dbContext.Dispose();
}
