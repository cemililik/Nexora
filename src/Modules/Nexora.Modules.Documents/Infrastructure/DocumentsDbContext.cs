using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Persistence;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Infrastructure;

/// <summary>
/// EF Core DbContext for the Documents module. Schema-per-tenant via BaseDbContext.
/// </summary>
public sealed class DocumentsDbContext(
    DbContextOptions<DocumentsDbContext> options,
    ITenantContextAccessor tenantContextAccessor,
    DomainEventDispatcher? domainEventDispatcher = null)
    : BaseDbContext(options, tenantContextAccessor, domainEventDispatcher)
{
    /// <summary>Gets the folders set.</summary>
    public DbSet<Folder> Folders => Set<Folder>();
    /// <summary>Gets the documents set.</summary>
    public DbSet<Document> Documents => Set<Document>();
    /// <summary>Gets the document versions set.</summary>
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    /// <summary>Gets the document accesses set.</summary>
    public DbSet<DocumentAccess> DocumentAccesses => Set<DocumentAccess>();
    /// <summary>Gets the signature requests set.</summary>
    public DbSet<SignatureRequest> SignatureRequests => Set<SignatureRequest>();
    /// <summary>Gets the signature recipients set.</summary>
    public DbSet<SignatureRecipient> SignatureRecipients => Set<SignatureRecipient>();
    /// <summary>Gets the document templates set.</summary>
    public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentsDbContext).Assembly);
        ApplySoftDeleteFilters(modelBuilder);
    }
}
