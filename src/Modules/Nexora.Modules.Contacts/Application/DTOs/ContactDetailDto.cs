namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>Detail DTO for contact get-by-id views, including addresses and tags.</summary>
public sealed record ContactDetailDto(
    Guid Id,
    string Type,
    string? Title,
    string? FirstName,
    string? LastName,
    string DisplayName,
    string? CompanyName,
    string? Email,
    string? Phone,
    string? Mobile,
    string? Website,
    string? TaxId,
    string Language,
    string Currency,
    string Source,
    string Status,
    Guid? MergedIntoId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ContactAddressDto> Addresses,
    IReadOnlyList<ContactTagSummaryDto> Tags);

/// <summary>Address DTO nested in contact detail.</summary>
public sealed record ContactAddressDto(
    Guid Id,
    string Type,
    string Street1,
    string? Street2,
    string City,
    string? State,
    string? PostalCode,
    string CountryCode,
    bool IsPrimary);

/// <summary>Tag summary nested in contact detail.</summary>
public sealed record ContactTagSummaryDto(
    Guid TagId,
    string Name,
    string Category,
    string? Color);
