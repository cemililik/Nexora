using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Contacts.Domain.ValueObjects;

/// <summary>
/// Value object encapsulating contact name logic.
/// Computes display name from first name, last name, and optional company name.
/// </summary>
public sealed record ContactName
{
    public string? FirstName { get; }
    public string? LastName { get; }
    public string? CompanyName { get; }
    public string DisplayName { get; }

    private ContactName(string? firstName, string? lastName, string? companyName)
    {
        FirstName = firstName?.Trim();
        LastName = lastName?.Trim();
        CompanyName = companyName?.Trim();
        DisplayName = ComputeDisplayName();
    }

    /// <summary>Creates a ContactName for an individual contact.</summary>
    public static ContactName ForIndividual(string firstName, string lastName)
        => new(firstName, lastName, null);

    /// <summary>Creates a ContactName for an organization contact.</summary>
    public static ContactName ForOrganization(string companyName)
        => new(null, null, companyName);

    /// <summary>Creates a ContactName with all fields.</summary>
    public static ContactName Create(string? firstName, string? lastName, string? companyName)
    {
        if (string.IsNullOrWhiteSpace(firstName) &&
            string.IsNullOrWhiteSpace(lastName) &&
            string.IsNullOrWhiteSpace(companyName))
            throw new DomainException("lockey_contacts_error_name_required");

        return new(firstName, lastName, companyName);
    }

    private string ComputeDisplayName()
    {
        var hasFirst = !string.IsNullOrWhiteSpace(FirstName);
        var hasLast = !string.IsNullOrWhiteSpace(LastName);
        var hasCompany = !string.IsNullOrWhiteSpace(CompanyName);

        if (hasFirst && hasLast)
            return $"{FirstName} {LastName}";

        if (hasFirst)
            return FirstName!;

        if (hasLast)
            return LastName!;

        if (hasCompany)
            return CompanyName!;

        return string.Empty;
    }
}
