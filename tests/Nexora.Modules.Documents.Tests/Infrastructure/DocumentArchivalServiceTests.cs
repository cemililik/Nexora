using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.Modules.Documents.Infrastructure.Services;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Infrastructure;

public sealed class DocumentArchivalServiceTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly DocumentArchivalService _service;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public DocumentArchivalServiceTests()
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(_tenantId.ToString(), _orgId.ToString(), _userId.ToString());

        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DocumentsDbContext(options, accessor);
        _service = new DocumentArchivalService(_dbContext, NullLogger<DocumentArchivalService>.Instance);
    }

    private async Task<Document> SeedDocumentAsync()
    {
        var folder = Folder.Create(_tenantId, _orgId, "Test", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var doc = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "contract.pdf", "application/pdf", 2048, "key/contract.pdf");
        await _dbContext.Documents.AddAsync(doc);
        await _dbContext.SaveChangesAsync();
        return doc;
    }

    [Fact]
    public async Task ArchiveSignedDocument_ValidDocument_MovesToSignedDocumentsFolder()
    {
        // Arrange
        var doc = await SeedDocumentAsync();
        var requestId = SignatureRequestId.New();

        // Act
        await _service.ArchiveSignedDocumentAsync(doc.Id, requestId, _tenantId, _orgId);

        // Assert
        var updated = await _dbContext.Documents.FirstAsync(d => d.Id == doc.Id);
        updated.Status.Should().Be(DocumentStatus.Archived);

        var signedFolder = await _dbContext.Folders
            .FirstOrDefaultAsync(f => f.Name == DocumentArchivalService.SignedDocumentsFolderName && f.IsSystem);
        signedFolder.Should().NotBeNull();
        updated.FolderId.Should().Be(signedFolder!.Id);
    }

    [Fact]
    public async Task ArchiveSignedDocument_NoSystemFolder_CreatesSystemFolder()
    {
        // Arrange
        var doc = await SeedDocumentAsync();
        var requestId = SignatureRequestId.New();

        // Act
        await _service.ArchiveSignedDocumentAsync(doc.Id, requestId, _tenantId, _orgId);

        // Assert
        var signedFolder = await _dbContext.Folders
            .FirstOrDefaultAsync(f =>
                f.Name == DocumentArchivalService.SignedDocumentsFolderName &&
                f.IsSystem &&
                f.TenantId == _tenantId &&
                f.OrganizationId == _orgId);
        signedFolder.Should().NotBeNull();
    }

    [Fact]
    public async Task ArchiveSignedDocument_SystemFolderExists_ReusesFolder()
    {
        // Arrange
        var doc1 = await SeedDocumentAsync();
        var doc2 = await SeedDocumentAsync();
        var requestId1 = SignatureRequestId.New();
        var requestId2 = SignatureRequestId.New();

        // Act
        await _service.ArchiveSignedDocumentAsync(doc1.Id, requestId1, _tenantId, _orgId);
        await _service.ArchiveSignedDocumentAsync(doc2.Id, requestId2, _tenantId, _orgId);

        // Assert — only one system folder should exist
        var signedFolders = await _dbContext.Folders
            .Where(f => f.Name == DocumentArchivalService.SignedDocumentsFolderName && f.IsSystem)
            .ToListAsync();
        signedFolders.Should().HaveCount(1);

        var updated1 = await _dbContext.Documents.FirstAsync(d => d.Id == doc1.Id);
        var updated2 = await _dbContext.Documents.FirstAsync(d => d.Id == doc2.Id);
        updated1.FolderId.Should().Be(updated2.FolderId);
    }

    [Fact]
    public async Task ArchiveSignedDocument_DocumentNotFound_DoesNotThrow()
    {
        // Arrange
        var fakeDocId = DocumentId.New();
        var requestId = SignatureRequestId.New();

        // Act
        var act = () => _service.ArchiveSignedDocumentAsync(fakeDocId, requestId, _tenantId, _orgId);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ArchiveSignedDocument_DifferentTenant_DoesNotArchive()
    {
        // Arrange
        var doc = await SeedDocumentAsync();
        var requestId = SignatureRequestId.New();
        var otherTenantId = Guid.NewGuid();

        // Act
        await _service.ArchiveSignedDocumentAsync(doc.Id, requestId, otherTenantId, _orgId);

        // Assert — document should not be archived (tenant mismatch)
        var updated = await _dbContext.Documents.FirstAsync(d => d.Id == doc.Id);
        updated.Status.Should().Be(DocumentStatus.Active);
    }

    public void Dispose() => _dbContext.Dispose();
}
