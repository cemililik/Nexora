using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Documents.Domain.Entities;

/// <summary>
/// Aggregate root representing a document template with variable substitution support.
/// </summary>
public sealed class DocumentTemplate : AuditableEntity<DocumentTemplateId>, IAggregateRoot
{
    /// <summary>Gets the tenant identifier.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Gets the organization identifier.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Gets the template name.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Gets the template category.</summary>
    public TemplateCategory Category { get; private set; }

    /// <summary>Gets the template output format.</summary>
    public TemplateFormat Format { get; private set; }

    /// <summary>Gets the storage key for the template file.</summary>
    public string TemplateStorageKey { get; private set; } = default!;

    /// <summary>Gets the JSON variable definitions for the template.</summary>
    public string? VariableDefinitions { get; private set; }

    /// <summary>Gets a value indicating whether the template is active.</summary>
    public bool IsActive { get; private set; }

    private DocumentTemplate() { }

    /// <summary>Creates a new DocumentTemplate instance.</summary>
    public static DocumentTemplate Create(
        Guid tenantId,
        Guid organizationId,
        string name,
        TemplateCategory category,
        TemplateFormat format,
        string templateStorageKey,
        string? variableDefinitions = null)
    {
        if (tenantId == Guid.Empty) throw new DomainException("lockey_documents_error_invalid_tenant");
        if (organizationId == Guid.Empty) throw new DomainException("lockey_documents_error_invalid_organization");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("lockey_documents_error_template_name_required");
        if (string.IsNullOrWhiteSpace(templateStorageKey)) throw new DomainException("lockey_documents_error_template_storage_key_required");

        return new DocumentTemplate
        {
            Id = DocumentTemplateId.New(),
            TenantId = tenantId,
            OrganizationId = organizationId,
            Name = name.Trim(),
            Category = category,
            Format = format,
            TemplateStorageKey = templateStorageKey,
            VariableDefinitions = variableDefinitions,
            IsActive = true
        };
    }

    /// <summary>Updates the template metadata.</summary>
    public void Update(string name, TemplateCategory category, TemplateFormat format, string? variableDefinitions)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("lockey_documents_error_template_name_required");

        Name = name.Trim();
        Category = category;
        Format = format;
        VariableDefinitions = variableDefinitions;
    }

    /// <summary>Activates the template.</summary>
    public void Activate() => IsActive = true;

    /// <summary>Deactivates the template.</summary>
    public void Deactivate() => IsActive = false;
}
