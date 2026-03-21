using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Application;

public sealed class ArchiveDocumentTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public ArchiveDocumentTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new DocumentsDbContext(options, _tenantAccessor);
    }

    private async Task<Document> SeedDocumentAsync(bool archive = false)
    {
        var folder = Folder.Create(_tenantId, _orgId, "Folder", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var document = Document.Create(_tenantId, _orgId, folder.Id, _userId,
            "test.pdf", "application/pdf", 1024, "storage/test.pdf");
        if (archive) document.Archive();
        await _dbContext.Documents.AddAsync(document);
        await _dbContext.SaveChangesAsync();
        return document;
    }

    [Fact]
    public async Task Handle_ActiveDocument_ArchivesSuccessfully()
    {
        var document = await SeedDocumentAsync();
        var handler = CreateHandler();
        var command = new ArchiveDocumentCommand(document.Id.Value);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await _dbContext.Documents.FindAsync(document.Id);
        updated!.Status.Should().Be(DocumentStatus.Archived);
    }

    [Fact]
    public async Task Handle_AlreadyArchivedDocument_ReturnsFailure()
    {
        var document = await SeedDocumentAsync(archive: true);
        var handler = CreateHandler();
        var command = new ArchiveDocumentCommand(document.Id.Value);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_documents_error_already_archived");
    }

    [Fact]
    public async Task Handle_NonExistentDocument_ReturnsFailure()
    {
        var handler = CreateHandler();
        var command = new ArchiveDocumentCommand(Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DocumentInDifferentTenant_ReturnsFailure()
    {
        var otherTenantId = Guid.NewGuid();
        var folder = Folder.Create(otherTenantId, Guid.NewGuid(), "OtherFolder", Guid.NewGuid());
        await _dbContext.Folders.AddAsync(folder);
        var document = Document.Create(otherTenantId, Guid.NewGuid(), folder.Id, Guid.NewGuid(),
            "other.pdf", "application/pdf", 100, "key");
        await _dbContext.Documents.AddAsync(document);
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        var command = new ArchiveDocumentCommand(document.Id.Value);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    public void Dispose() => _dbContext.Dispose();

    private ArchiveDocumentHandler CreateHandler() =>
        new(_dbContext, _tenantAccessor, NullLogger<ArchiveDocumentHandler>.Instance);

    private ITenantContextAccessor CreateTenantAccessor(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString(), _userId.ToString());
        return accessor;
    }
}
