using Nexora.Modules.Identity.Domain.Constants;
using Nexora.Modules.Identity.Domain.Events;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Identity.Domain.Entities;

/// <summary>Represents an organization within a tenant.</summary>
public sealed class Organization : AuditableEntity<OrganizationId>, IAggregateRoot
{
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string? LogoUrl { get; private set; }
    public string Timezone { get; private set; } = "UTC";
    public string DefaultCurrency { get; private set; } = "USD";
    public string DefaultLanguage { get; private set; } = "en";
    /// <summary>IETF locale tag for number/date formatting (e.g. "en-US", "tr-TR"). Defaults to "en-US".</summary>
    public string DefaultLocale { get; private set; } = "en-US";
    public bool IsActive { get; private set; } = true;

    private readonly List<Department> _departments = [];
    public IReadOnlyList<Department> Departments => _departments.AsReadOnly();

    private Organization() { }

    /// <summary>Creates a new organization for a tenant.</summary>
    public static Organization Create(TenantId tenantId, string name, string slug)
    {
        var org = new Organization
        {
            Id = OrganizationId.New(),
            TenantId = tenantId,
            Name = name,
            Slug = slug.ToLowerInvariant()
        };
        org.AddDomainEvent(new OrganizationCreatedEvent(org.Id, tenantId));
        return org;
    }

    /// <summary>Updates the organization's profile settings.</summary>
    public void Update(string name, string timezone, string currency, string language, string locale)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("lockey_identity_error_org_name_required");
        if (string.IsNullOrWhiteSpace(timezone))
            throw new DomainException("lockey_identity_validation_org_timezone_required");
        if (string.IsNullOrWhiteSpace(currency))
            throw new DomainException("lockey_identity_validation_org_currency_required");
        if (string.IsNullOrWhiteSpace(language))
            throw new DomainException("lockey_identity_validation_org_language_required");
        if (string.IsNullOrWhiteSpace(locale))
            throw new DomainException("lockey_identity_validation_org_locale_required");
        if (!LocaleConstants.SupportedLocales.Contains(locale))
            throw new DomainException("lockey_identity_error_org_locale_unsupported");
        if (!LocaleConstants.SupportedTimezones.Contains(timezone))
            throw new DomainException("lockey_identity_error_org_timezone_unsupported");
        if (!LocaleConstants.SupportedCurrencies.Contains(currency))
            throw new DomainException("lockey_identity_error_org_currency_unsupported");
        if (!LocaleConstants.SupportedLanguages.Contains(language))
            throw new DomainException("lockey_identity_error_org_language_unsupported");

        Name = name;
        Timezone = timezone;
        DefaultCurrency = currency;
        DefaultLanguage = language;
        DefaultLocale = locale;
    }

    /// <summary>Deactivates the organization.</summary>
    public void Deactivate() => IsActive = false;
    /// <summary>Activates the organization.</summary>
    public void Activate() => IsActive = true;
}
