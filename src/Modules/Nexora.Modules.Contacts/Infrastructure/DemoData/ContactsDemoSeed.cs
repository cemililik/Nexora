using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Modules;

namespace Nexora.Modules.Contacts.Infrastructure.DemoData;

/// <summary>
/// T-029: Contacts module demo seeder. Materialises the contact rows
/// for the platform-default <c>general</c> + <c>ngo</c> scenarios when
/// the orchestrator (T-005's <see cref="IDemoDataSeeder"/>) calls
/// <see cref="ContactsModule.SeedDemoDataAsync"/>.
///
/// <para>
/// <b>Idempotency.</b> Both seeds key on lower-cased email — re-running
/// against an existing seed inserts zero new rows. The orchestrator's
/// marker table reduces the chance of re-invocation, but modules MUST
/// stay idempotent regardless (see <see cref="IModule.SeedDemoDataAsync"/>
/// docs).
/// </para>
///
/// <para>
/// <b>Scope.</b> Pure Contact rows. Donations, sponsorships, CRM leads,
/// and Finance entries belong to other modules and ship as Phase 2 / Phase
/// 3a per-module seed tasks. The donor-cohort shape stored on each
/// <c>ngo</c> contact (recurring vs. one-time vs. lapsed vs. major) is
/// captured via <c>tenant_</c>-prefixed metadata that the Fundraising
/// seed will read when it lands.
/// </para>
/// </summary>
internal static class ContactsDemoSeed
{
    /// <summary>Org-id sentinel used when no specific org context is set on the seed call.</summary>
    private static readonly Guid DefaultOrganizationId = Guid.Empty;

    /// <summary>
    /// Entry point — dispatches on the scenario string. Unknown scenarios
    /// are a no-op (matches the per-module-skip semantics in the seeder
    /// docs: a module MAY support a subset and skip otherwise).
    /// </summary>
    public static Task SeedAsync(TenantDemoSeedContext context, CancellationToken ct) =>
        context.Scenario switch
        {
            "general" => SeedGeneralAsync(context, ct),
            "ngo" => SeedNgoAsync(context, ct),
            _ => Task.CompletedTask,
        };

    private static async Task SeedGeneralAsync(TenantDemoSeedContext context, CancellationToken ct)
    {
        var dbContext = context.ScopedServices.GetRequiredService<ContactsDbContext>();
        var tenantId = Guid.Parse(context.TenantId);
        var orgId = ParseOrgIdOrDefault(context.OrganizationId);

        // ~20 business contacts: a mix of staff (Individual + Source.Manual)
        // and clients across regions (Individual + Company). Names drawn
        // from public demo-name lists; emails on example.com so they cannot
        // be mistaken for real addresses. Each contact carries a deterministic
        // email so re-runs match existing rows on the natural key.
        var seedRows = new (ContactType Type, string? First, string? Last, string? Company, string Email, string? Phone, string? Title)[]
        {
            // Staff (US team)
            (ContactType.Individual, "Alex",   "Morgan",   null, "alex.morgan@example.com",   "+1-415-555-0101", "Founder"),
            (ContactType.Individual, "Priya",  "Patel",    null, "priya.patel@example.com",   "+1-415-555-0102", "VP Engineering"),
            (ContactType.Individual, "Marcus", "Lee",      null, "marcus.lee@example.com",    "+1-415-555-0103", "Customer Success Manager"),
            // Staff (EU team)
            (ContactType.Individual, "Léa",    "Bernard",  null, "lea.bernard@example.com",   "+33-1-55-55-0104", "Product Designer"),
            (ContactType.Individual, "Stefan", "Müller",   null, "stefan.muller@example.com", "+49-30-555-0105",  "Sales Director"),
            // Staff (TR team)
            (ContactType.Individual, "Ayşe",   "Demir",    null, "ayse.demir@example.com",    "+90-212-555-0106", "Operations Lead"),
            (ContactType.Individual, "Mehmet", "Kaya",     null, "mehmet.kaya@example.com",   "+90-212-555-0107", "Backend Engineer"),
            // SMB clients (Individual)
            (ContactType.Individual, "Olivia", "Chen",     null, "olivia.chen@example.com",   "+1-628-555-0108", "Owner — Chen Bakery"),
            (ContactType.Individual, "Ben",    "Cohen",    null, "ben.cohen@example.com",     "+1-628-555-0109", "Solo Consultant"),
            (ContactType.Individual, "Yuki",   "Tanaka",   null, "yuki.tanaka@example.com",   "+81-3-5555-0110", "Owner — Tanaka Studio"),
            // Companies (Type=Company; first/last null, companyName populated)
            (ContactType.Organization,    null, null, "Acme Robotics",       "ar.contact@example.com",  "+1-415-555-0201", null),
            (ContactType.Organization,    null, null, "Globex Logistics",    "globex@example.com",       "+1-415-555-0202", null),
            (ContactType.Organization,    null, null, "Initech Software",    "info@initech.example.com", "+1-415-555-0203", null),
            (ContactType.Organization,    null, null, "Soylent Foods Co",    "hello@soylent.example.com","+1-415-555-0204", null),
            (ContactType.Organization,    null, null, "Umbrella Health",     "team@umbrella.example.com","+1-415-555-0205", null),
            (ContactType.Organization,    null, null, "Wonka Confections",   "candy@wonka.example.com",  "+44-20-7555-0206", null),
            (ContactType.Organization,    null, null, "Stark Industries TR", "tr@stark.example.com",     "+90-212-555-0207", null),
            (ContactType.Organization,    null, null, "Pied Piper",          "pp@piedpiper.example.com", "+1-628-555-0208", null),
            (ContactType.Organization,    null, null, "Hooli",               "biz@hooli.example.com",    "+1-628-555-0209", null),
            (ContactType.Organization,    null, null, "Cyberdyne Systems",   "ops@cyberdyne.example.com","+1-628-555-0210", null),
        };

        await UpsertContactsAsync(dbContext, tenantId, orgId, seedRows, ct);
    }

    private static async Task SeedNgoAsync(TenantDemoSeedContext context, CancellationToken ct)
    {
        var dbContext = context.ScopedServices.GetRequiredService<ContactsDbContext>();
        var tenantId = Guid.Parse(context.TenantId);
        var orgId = ParseOrgIdOrDefault(context.OrganizationId);

        // ~30 donor contacts spanning four cohort shapes:
        //   one-time:  10 contacts — gave once, no recurring relationship
        //   recurring: 10 contacts — monthly recurring donors
        //   lapsed:     5 contacts — gave previously, none in last 24 months
        //   major:      5 contacts — high-value donors (single or aggregated)
        // Cohort labels live in the email's local part for now (e.g.
        // "donor-recurring-01@example.org") so the future Fundraising
        // seed task can join its donation rows on the matching contacts
        // by parsing the local part. Names use a mix of US/EU/TR/JP
        // origins so locale formatting is exercised.
        var donors = new List<(ContactType Type, string? First, string? Last, string? Company, string Email, string? Phone)>();

        // one-time donors
        var oneTimeNames = new (string First, string Last)[]
        {
            ("Sarah",  "Williams"), ("David",  "Johnson"), ("Aiko",   "Nakamura"),
            ("Carlos", "Garcia"),   ("Emma",   "Brown"),   ("Hassan", "Yıldız"),
            ("Sophie", "Dubois"),   ("Hiroshi","Sato"),    ("Maria",  "Rossi"),
            ("Kai",    "Tanaka"),
        };
        for (int i = 0; i < oneTimeNames.Length; i++)
        {
            var n = oneTimeNames[i];
            donors.Add((ContactType.Individual, n.First, n.Last, null,
                $"donor-onetime-{i + 1:D2}@example.org", $"+1-555-100-{i:D2}00"));
        }

        // recurring (monthly) donors
        var recurringNames = new (string First, string Last)[]
        {
            ("Jonas",   "Schmidt"),  ("Fatima", "Al-Hassan"), ("Yuki",   "Watanabe"),
            ("Liam",    "Walsh"),    ("Olu",    "Adebayo"),   ("Daria",  "Petrova"),
            ("Henrik",  "Andersen"), ("Mei",    "Lin"),       ("Khalil", "Mansour"),
            ("Aisha",   "Khan"),
        };
        for (int i = 0; i < recurringNames.Length; i++)
        {
            var n = recurringNames[i];
            donors.Add((ContactType.Individual, n.First, n.Last, null,
                $"donor-recurring-{i + 1:D2}@example.org", $"+1-555-200-{i:D2}00"));
        }

        // lapsed donors (gave once 25+ months ago, no follow-up)
        var lapsedNames = new (string First, string Last)[]
        {
            ("Robert",  "Taylor"),    ("Elif", "Çelik"),
            ("Lukas",   "Berger"),    ("Niamh", "O'Connor"),
            ("Tariq",   "Mahmoud"),
        };
        for (int i = 0; i < lapsedNames.Length; i++)
        {
            var n = lapsedNames[i];
            donors.Add((ContactType.Individual, n.First, n.Last, null,
                $"donor-lapsed-{i + 1:D2}@example.org", $"+1-555-300-{i:D2}00"));
        }

        // major donors — higher-value givers; some Individual, some Company
        donors.Add((ContactType.Individual, "Eleanor", "Whitfield", null,
            "donor-major-01@example.org", "+1-555-400-0100"));
        donors.Add((ContactType.Individual, "Hideo", "Yamamoto", null,
            "donor-major-02@example.org", "+81-3-5555-0400"));
        donors.Add((ContactType.Organization, null, null, "Vanguard Foundation",
            "donor-major-03@example.org", "+1-555-400-0300"));
        donors.Add((ContactType.Organization, null, null, "Çelik Holding Charitable Trust",
            "donor-major-04@example.org", "+90-212-555-0400"));
        donors.Add((ContactType.Organization, null, null, "Müller-Bernard Family Office",
            "donor-major-05@example.org", "+49-89-555-0500"));

        var seedRows = donors
            .Select(d => (d.Type, d.First, d.Last, d.Company, d.Email, d.Phone, (string?)null))
            .ToArray();
        await UpsertContactsAsync(dbContext, tenantId, orgId, seedRows, ct);
    }

    private static async Task UpsertContactsAsync(
        ContactsDbContext dbContext,
        Guid tenantId,
        Guid orgId,
        IReadOnlyList<(ContactType Type, string? First, string? Last, string? Company, string Email, string? Phone, string? Title)> rows,
        CancellationToken ct)
    {
        // Bulk pre-fetch existing emails for this tenant so the per-row
        // existence check is a HashSet lookup, not N round-trips.
        var emails = rows.Select(r => r.Email.ToLowerInvariant()).ToList();
        var existing = await dbContext.Contacts
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Email != null && emails.Contains(c.Email))
            .Select(c => c.Email!)
            .ToListAsync(ct);
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var row in rows)
        {
            var emailKey = row.Email.ToLowerInvariant();
            if (existingSet.Contains(emailKey)) continue;

            var contact = Contact.Create(
                tenantId: tenantId,
                organizationId: orgId,
                type: row.Type,
                firstName: row.First,
                lastName: row.Last,
                companyName: row.Company,
                email: row.Email,
                phone: row.Phone,
                source: ContactSource.Manual,
                title: row.Title);
            await dbContext.Contacts.AddAsync(contact, ct);
            added++;
        }

        if (added > 0)
        {
            await dbContext.SaveChangesAsync(ct);
        }
    }

    private static Guid ParseOrgIdOrDefault(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return DefaultOrganizationId;
        return Guid.TryParse(raw, out var parsed) ? parsed : DefaultOrganizationId;
    }
}
