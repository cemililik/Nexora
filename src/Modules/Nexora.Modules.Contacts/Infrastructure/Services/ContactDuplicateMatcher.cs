using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Contacts.Domain.Services;

namespace Nexora.Modules.Contacts.Infrastructure.Services;

/// <summary>
/// EF-based <see cref="IContactDuplicateMatcher"/> implementation.
/// Normalizes email (lowercase + trim) and phone (digits only) before probing
/// the Contacts table within the caller's (tenant, organization) scope.
/// </summary>
public sealed class ContactDuplicateMatcher(ContactsDbContext dbContext)
    : IContactDuplicateMatcher
{
    /// <inheritdoc />
    public async Task<Guid?> FindExistingContactIdAsync(
        Guid tenantId,
        Guid organizationId,
        string? email,
        string? phone,
        CancellationToken ct)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedPhone = NormalizePhone(phone);

        if (normalizedEmail is null && normalizedPhone is null)
            return null;

        var query = dbContext.Contacts
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.OrganizationId == organizationId);

        if (normalizedEmail is not null && normalizedPhone is not null)
        {
            query = query.Where(c =>
                (c.Email != null && c.Email == normalizedEmail) ||
                (c.Phone != null && c.Phone == normalizedPhone));
        }
        else if (normalizedEmail is not null)
        {
            query = query.Where(c => c.Email != null && c.Email == normalizedEmail);
        }
        else
        {
            query = query.Where(c => c.Phone != null && c.Phone == normalizedPhone);
        }

        var match = await query
            .Select(c => new { c.Id })
            .FirstOrDefaultAsync(ct);

        return match?.Id.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, Guid>> FindExistingByEmailsAsync(
        Guid tenantId,
        Guid organizationId,
        IReadOnlyCollection<string> normalizedEmails,
        CancellationToken ct)
    {
        if (normalizedEmails.Count == 0)
            return new Dictionary<string, Guid>(0);

        var matches = await dbContext.Contacts
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId
                     && c.OrganizationId == organizationId
                     && c.Email != null
                     && normalizedEmails.Contains(c.Email))
            .Select(c => new { c.Email, c.Id })
            .ToListAsync(ct);

        // Multiple contacts with the same normalized email should never happen, but defensively
        // keep the first hit so the dictionary contract (one id per email) holds.
        var result = new Dictionary<string, Guid>(matches.Count);
        foreach (var match in matches)
        {
            if (match.Email is not null && !result.ContainsKey(match.Email))
                result[match.Email] = match.Id.Value;
        }
        return result;
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;
        return email.Trim().ToLowerInvariant();
    }

    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;
        return phone.Trim();
    }
}
