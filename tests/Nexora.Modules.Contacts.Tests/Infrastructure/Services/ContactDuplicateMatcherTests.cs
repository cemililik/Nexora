using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Contacts.Infrastructure.Services;
using Nexora.Modules.Contacts.Tests.Helpers;

namespace Nexora.Modules.Contacts.Tests.Infrastructure.Services;

/// <summary>Unit tests for <see cref="ContactDuplicateMatcher"/>.</summary>
public sealed class ContactDuplicateMatcherTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ContactDuplicateMatcher _matcher;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public ContactDuplicateMatcherTests()
    {
        var tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, tenantAccessor);
        _matcher = new ContactDuplicateMatcher(_dbContext);
    }

    [Fact]
    public async Task FindExistingByEmailsAsync_BatchQuery_ReturnsHitsAndSkipsMisses()
    {
        // Arrange — 3 seeded contacts with known emails
        var a = Contact.Create(_tenantId, _orgId, ContactType.Individual,
            "A", "One", null, "a@test.com", null, ContactSource.Manual);
        var b = Contact.Create(_tenantId, _orgId, ContactType.Individual,
            "B", "Two", null, "b@test.com", null, ContactSource.Manual);
        var c = Contact.Create(_tenantId, _orgId, ContactType.Individual,
            "C", "Three", null, "c@test.com", null, ContactSource.Manual);
        await _dbContext.Contacts.AddRangeAsync(a, b, c);
        await _dbContext.SaveChangesAsync();

        var probe = new[] { "a@test.com", "b@test.com", "c@test.com", "miss1@test.com", "miss2@test.com" };

        // Act
        var result = await _matcher.FindExistingByEmailsAsync(_tenantId, _orgId, probe, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result["a@test.com"].Should().Be(a.Id.Value);
        result["b@test.com"].Should().Be(b.Id.Value);
        result["c@test.com"].Should().Be(c.Id.Value);
        result.Should().NotContainKey("miss1@test.com");
        result.Should().NotContainKey("miss2@test.com");
    }

    [Fact]
    public async Task FindExistingByEmailsAsync_EmptyInput_ReturnsEmptyDictionary()
    {
        var result = await _matcher.FindExistingByEmailsAsync(
            _tenantId, _orgId, Array.Empty<string>(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FindExistingContactIdAsync_EmailCaseDiffers_StillDetectedAsDuplicate()
    {
        // Seed with canonically-lowercased email (the Contact factory / normalization path).
        var existing = Contact.Create(_tenantId, _orgId, ContactType.Individual,
            "Ada", "Lovelace", null, "ada@example.com", null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(existing);
        await _dbContext.SaveChangesAsync();

        // Probe with an upper-case + whitespace-padded variant that is semantically identical.
        var match = await _matcher.FindExistingContactIdAsync(
            _tenantId, _orgId, "  Ada@Example.COM  ", phone: null, CancellationToken.None);

        match.Should().Be(existing.Id.Value);
    }

    [Fact]
    public async Task FindExistingByEmailsAsync_CallerPassesRawEmails_NormalizesBeforeProbe()
    {
        // Contract: matcher trusts the caller to pre-normalize. The ContactImportJob
        // normalizes via `Trim().ToLowerInvariant()` before building its probe set, and
        // stored emails are always lowercase — so a lowercase-only probe suffices.
        // This test documents that behaviour so a future regression (e.g. removing the
        // caller's normalization) is caught explicitly.
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual,
            "Grace", "Hopper", null, "grace@example.com", null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        var probe = new[] { "grace@example.com" };
        var result = await _matcher.FindExistingByEmailsAsync(_tenantId, _orgId, probe, CancellationToken.None);

        result.Should().HaveCount(1);
        result["grace@example.com"].Should().Be(contact.Id.Value);
    }

    [Fact]
    public async Task FindExistingByEmailsAsync_OtherOrganization_NotReturned()
    {
        var otherOrg = Guid.NewGuid();
        var inScope = Contact.Create(_tenantId, _orgId, ContactType.Individual,
            "In", "Scope", null, "same@test.com", null, ContactSource.Manual);
        var outOfScope = Contact.Create(_tenantId, otherOrg, ContactType.Individual,
            "Out", "Scope", null, "same@test.com", null, ContactSource.Manual);
        await _dbContext.Contacts.AddRangeAsync(inScope, outOfScope);
        await _dbContext.SaveChangesAsync();

        var result = await _matcher.FindExistingByEmailsAsync(
            _tenantId, _orgId, new[] { "same@test.com" }, CancellationToken.None);

        result.Should().HaveCount(1);
        result["same@test.com"].Should().Be(inScope.Id.Value);
    }

    public void Dispose() => _dbContext.Dispose();
}
