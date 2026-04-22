using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Infrastructure.Persistence.Inbox;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Modules.Identity.Infrastructure.IntegrationEvents;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;
using NSubstitute;

namespace Nexora.Modules.Identity.Tests.Infrastructure.IntegrationEvents;

public sealed class ContactGdprDeletedIntegrationEventHandlerTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();
    private readonly TenantId _tenantId = TenantId.New();

    public ContactGdprDeletedIntegrationEventHandlerTests()
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(_tenantId.Value.ToString());
        _tenantAccessor = accessor;

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    public void Dispose() => _dbContext.Dispose();

    private ContactGdprDeletedIntegrationEventHandler CreateHandler() =>
        new(_dbContext, new InboxGuard<IdentityDbContext>(_dbContext), _outbox,
            NullLogger<ContactGdprDeletedIntegrationEventHandler>.Instance);

    [Fact]
    public async Task HandleAsync_UnlinksAllMatchingUsers_EmitsGdprReason_LeavesOthersAlone()
    {
        var contactId = Guid.NewGuid();
        var otherContact = Guid.NewGuid();

        var u1 = User.Create(_tenantId, "kc-1", "u1@test.com", "U", "One");
        u1.LinkContact(contactId, Guid.NewGuid());

        var u2 = User.Create(_tenantId, "kc-2", "u2@test.com", "U", "Two");
        u2.LinkContact(contactId, Guid.NewGuid());

        var u3 = User.Create(_tenantId, "kc-3", "u3@test.com", "U", "Three");
        u3.LinkContact(otherContact, Guid.NewGuid());

        _dbContext.Users.AddRange(u1, u2, u3);
        await _dbContext.SaveChangesAsync();

        var @event = new ContactGdprDeletedIntegrationEvent
        {
            TenantId = _tenantId.Value.ToString(),
            ContactId = contactId,
            Reason = "data subject request",
            Mode = "hard_deleted",
            DeletedAtUtc = DateTime.UtcNow,
            ErasedByUserId = Guid.NewGuid()
        };

        await CreateHandler().HandleAsync(@event, CancellationToken.None);

        (await _dbContext.Users.FindAsync(u1.Id))!.ContactId.Should().BeNull();
        (await _dbContext.Users.FindAsync(u2.Id))!.ContactId.Should().BeNull();
        (await _dbContext.Users.FindAsync(u3.Id))!.ContactId.Should().Be(otherContact);

        await _outbox.Received(2).EnqueueAsync(
            Arg.Is<UserContactUnlinkedIntegrationEvent>(e => e.Reason == "gdpr_erasure"),
            Arg.Any<CancellationToken>());

        (await _dbContext.InboxMessages.AnyAsync(m => m.EventId == @event.EventId))
            .Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_DuplicateEvent_IsSkipped()
    {
        var contactId = Guid.NewGuid();
        var user = User.Create(_tenantId, "kc-1", "u@test.com", "U", "One");
        user.LinkContact(contactId, Guid.NewGuid());
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var @event = new ContactGdprDeletedIntegrationEvent
        {
            TenantId = _tenantId.Value.ToString(),
            ContactId = contactId,
            Reason = "data subject request",
            Mode = "hard_deleted",
            DeletedAtUtc = DateTime.UtcNow,
            ErasedByUserId = Guid.NewGuid()
        };

        await CreateHandler().HandleAsync(@event, CancellationToken.None);
        _outbox.ClearReceivedCalls();

        await CreateHandler().HandleAsync(@event, CancellationToken.None);

        await _outbox.DidNotReceive().EnqueueAsync(
            Arg.Any<UserContactUnlinkedIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_CrossTenantUser_NotTouched()
    {
        var contactId = Guid.NewGuid();
        var otherTenant = TenantId.New();

        // Create a user in a different tenant linked to same contact
        // (contrived but guards against tenant leak).
        var foreignUser = User.Create(otherTenant, "kc-foreign", "f@test.com", "F", "User");
        foreignUser.LinkContact(contactId, Guid.NewGuid());
        _dbContext.Users.Add(foreignUser);
        await _dbContext.SaveChangesAsync();

        var @event = new ContactGdprDeletedIntegrationEvent
        {
            TenantId = _tenantId.Value.ToString(),
            ContactId = contactId,
            Reason = "data subject request",
            Mode = "hard_deleted",
            DeletedAtUtc = DateTime.UtcNow,
            ErasedByUserId = Guid.NewGuid()
        };

        await CreateHandler().HandleAsync(@event, CancellationToken.None);

        (await _dbContext.Users.FindAsync(foreignUser.Id))!.ContactId.Should().Be(contactId);
        await _outbox.DidNotReceive().EnqueueAsync(
            Arg.Any<UserContactUnlinkedIntegrationEvent>(), Arg.Any<CancellationToken>());
    }
}
