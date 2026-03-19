namespace Nexora.Modules.Identity.Application.DTOs;

/// <summary>
/// User summary returned from queries.
/// </summary>
public sealed record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string Status,
    DateTimeOffset? LastLoginAt);
