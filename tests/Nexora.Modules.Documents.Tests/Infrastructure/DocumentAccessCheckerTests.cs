using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.Modules.Documents.Infrastructure.Services;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Tests.Infrastructure;

public sealed class DocumentAccessCheckerTests : IDisposable
{
    private readonly DocumentsDbContext _dbContext;
    private readonly DocumentAccessChecker _checker;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public DocumentAccessCheckerTests()
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(_tenantId.ToString(), _orgId.ToString(), _userId.ToString());

        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DocumentsDbContext(options, accessor);
        _checker = new DocumentAccessChecker(_dbContext);
    }

    private async Task<Document> SeedDocumentAsync(Guid? uploadedByUserId = null)
    {
        var folder = Folder.Create(_tenantId, _orgId, "Test", _userId);
        await _dbContext.Folders.AddAsync(folder);
        var doc = Document.Create(_tenantId, _orgId, folder.Id, uploadedByUserId ?? _userId,
            "test.pdf", "application/pdf", 1024, "key/test.pdf");
        await _dbContext.Documents.AddAsync(doc);
        await _dbContext.SaveChangesAsync();
        return doc;
    }

    [Fact]
    public async Task HasAccessAsync_Owner_ReturnsTrue()
    {
        var doc = await SeedDocumentAsync();

        var result = await _checker.HasAccessAsync(doc.Id, _userId, _tenantId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasAccessAsync_NonOwnerWithoutAccess_ReturnsFalse()
    {
        var doc = await SeedDocumentAsync();
        var otherUser = Guid.NewGuid();

        var result = await _checker.HasAccessAsync(doc.Id, otherUser, _tenantId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasAccessAsync_NonOwnerWithExplicitAccess_ReturnsTrue()
    {
        var doc = await SeedDocumentAsync();
        var otherUser = Guid.NewGuid();
        var access = DocumentAccess.Create(doc.Id, otherUser, null, AccessPermission.View);
        await _dbContext.Set<DocumentAccess>().AddAsync(access);
        await _dbContext.SaveChangesAsync();

        var result = await _checker.HasAccessAsync(doc.Id, otherUser, _tenantId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasAccessAsync_DifferentTenant_ReturnsFalse()
    {
        var doc = await SeedDocumentAsync();
        var otherTenant = Guid.NewGuid();

        var result = await _checker.HasAccessAsync(doc.Id, _userId, otherTenant);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAccessFilter_OwnerDocs_ReturnsAll()
    {
        var doc1 = await SeedDocumentAsync();
        var doc2 = await SeedDocumentAsync();

        var query = _dbContext.Documents.AsQueryable();
        var filtered = _checker.ApplyAccessFilter(query, _userId, _tenantId);
        var result = await filtered.ToListAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ApplyAccessFilter_MixedAccess_ReturnsOnlyAccessible()
    {
        var ownedDoc = await SeedDocumentAsync();
        var otherDoc = await SeedDocumentAsync(uploadedByUserId: Guid.NewGuid());

        var query = _dbContext.Documents.AsQueryable();
        var filtered = _checker.ApplyAccessFilter(query, _userId, _tenantId);
        var result = await filtered.ToListAsync();

        result.Should().ContainSingle().Which.Id.Should().Be(ownedDoc.Id);
    }

    [Fact]
    public async Task ApplyAccessFilter_ExplicitAccess_IncludesDocument()
    {
        var otherDoc = await SeedDocumentAsync(uploadedByUserId: Guid.NewGuid());
        var access = DocumentAccess.Create(otherDoc.Id, _userId, null, AccessPermission.View);
        await _dbContext.Set<DocumentAccess>().AddAsync(access);
        await _dbContext.SaveChangesAsync();

        var query = _dbContext.Documents.AsQueryable();
        var filtered = _checker.ApplyAccessFilter(query, _userId, _tenantId);
        var result = await filtered.ToListAsync();

        result.Should().ContainSingle().Which.Id.Should().Be(otherDoc.Id);
    }

    [Fact]
    public async Task HasAccessAsync_NonOwnerWithRoleAccess_ReturnsTrue()
    {
        var doc = await SeedDocumentAsync();
        var otherUser = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var access = DocumentAccess.Create(doc.Id, null, roleId, AccessPermission.View);
        await _dbContext.Set<DocumentAccess>().AddAsync(access);
        await _dbContext.SaveChangesAsync();

        var result = await _checker.HasAccessAsync(doc.Id, otherUser, _tenantId, roleIds: [roleId]);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasAccessAsync_NonOwnerWithWrongRole_ReturnsFalse()
    {
        var doc = await SeedDocumentAsync();
        var otherUser = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var access = DocumentAccess.Create(doc.Id, null, roleId, AccessPermission.View);
        await _dbContext.Set<DocumentAccess>().AddAsync(access);
        await _dbContext.SaveChangesAsync();

        var result = await _checker.HasAccessAsync(doc.Id, otherUser, _tenantId, roleIds: [Guid.NewGuid()]);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAccessFilter_RoleBasedAccess_IncludesDocument()
    {
        var otherDoc = await SeedDocumentAsync(uploadedByUserId: Guid.NewGuid());
        var roleId = Guid.NewGuid();
        var access = DocumentAccess.Create(otherDoc.Id, null, roleId, AccessPermission.Edit);
        await _dbContext.Set<DocumentAccess>().AddAsync(access);
        await _dbContext.SaveChangesAsync();

        var query = _dbContext.Documents.AsQueryable();
        var filtered = _checker.ApplyAccessFilter(query, _userId, _tenantId, roleIds: [roleId]);
        var result = await filtered.ToListAsync();

        result.Should().ContainSingle().Which.Id.Should().Be(otherDoc.Id);
    }

    [Fact]
    public async Task HasAccessAsync_NoRolesProvided_DoesNotCheckRoles()
    {
        var doc = await SeedDocumentAsync();
        var otherUser = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var access = DocumentAccess.Create(doc.Id, null, roleId, AccessPermission.View);
        await _dbContext.Set<DocumentAccess>().AddAsync(access);
        await _dbContext.SaveChangesAsync();

        // No roleIds passed — should not match role-based access
        var result = await _checker.HasAccessAsync(doc.Id, otherUser, _tenantId);

        result.Should().BeFalse();
    }

    public void Dispose() => _dbContext.Dispose();
}
