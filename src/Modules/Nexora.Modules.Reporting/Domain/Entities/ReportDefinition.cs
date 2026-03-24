using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Reporting.Domain.Entities;

/// <summary>
/// Aggregate root representing a SQL-based report definition.
/// Scoped to a tenant and organization.
/// </summary>
public sealed class ReportDefinition : AuditableEntity<ReportDefinitionId>, IAggregateRoot
{
    public Guid TenantId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public string Module { get; private set; } = default!;
    public string? Category { get; private set; }
    public string QueryText { get; private set; } = default!;
    public string? Parameters { get; private set; } // JSON array of ReportParameterDefinition
    public ReportFormat DefaultFormat { get; private set; }
    public bool IsActive { get; private set; }

    private ReportDefinition() { }

    public static ReportDefinition Create(
        Guid tenantId,
        Guid organizationId,
        string name,
        string? description,
        string module,
        string? category,
        string queryText,
        string? parameters,
        ReportFormat defaultFormat)
    {
        return new ReportDefinition
        {
            Id = ReportDefinitionId.New(),
            TenantId = tenantId,
            OrganizationId = organizationId,
            Name = name.Trim(),
            Description = description?.Trim(),
            Module = module.Trim(),
            Category = category?.Trim(),
            QueryText = queryText,
            Parameters = parameters,
            DefaultFormat = defaultFormat,
            IsActive = true
        };
    }

    public void Update(
        string name,
        string? description,
        string module,
        string? category,
        string queryText,
        string? parameters,
        ReportFormat defaultFormat)
    {
        Name = name.Trim();
        Description = description?.Trim();
        Module = module.Trim();
        Category = category?.Trim();
        QueryText = queryText;
        Parameters = parameters;
        DefaultFormat = defaultFormat;
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("lockey_reporting_error_definition_already_inactive");
        IsActive = false;
    }

    public void Activate()
    {
        if (IsActive)
            throw new DomainException("lockey_reporting_error_definition_already_active");
        IsActive = true;
    }
}
