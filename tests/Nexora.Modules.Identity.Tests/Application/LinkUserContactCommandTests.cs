using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;
using NSubstitute;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class LinkUserContactCommandTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();
    private readonly TenantId _tenantId = TenantId.New();
    private readonly Guid _actorUserId = Guid.NewGuid();

    public LinkUserContactCommandTests()
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(_tenantId.Value.ToString(), userId: _actorUserId.ToString());
        _tenantAccessor = accessor;

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    public void Dispose() => _dbContext.Dispose();

    private LinkUserContactHandler CreateHandler() =>
        new(_dbContext, _tenantAccessor, _outbox, NullLogger<LinkUserContactHandler>.Instance);

    [Fact]
    public async Task Handle_ValidUser_LinksAndEmitsIntegrationEvent()
    {
        var user = User.Create(_tenantId, "kc-human-1", "u@test.com", "U", "One");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var contactId = Guid.NewGuid();
        var result = await CreateHandler().Handle(
            new LinkUserContactCommand(user.Id.Value, contactId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message!.Key.Should().Be("lockey_identity_user_link_contact_success");

        var persisted = await _dbContext.Users.FindAsync(user.Id);
        persisted!.ContactId.Should().Be(contactId);

        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<UserContactLinkedIntegrationEvent>(e => e.UserId == user.Id.Value && e.ContactId == contactId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SystemAccount_ReturnsFailure_WithoutEmitting()
    {
        var user = User.Create(_tenantId, "service-account-bot", "svc@test.com", "S", "A");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(
            new LinkUserContactCommand(user.Id.Value, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_user_link_contact_system_account_rejected");
        await _outbox.DidNotReceive().EnqueueAsync(Arg.Any<UserContactLinkedIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        var result = await CreateHandler().Handle(
            new LinkUserContactCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_user_link_contact_user_not_found");
    }

    [Fact]
    public async Task Handle_AlreadyLinked_ToSameContact_ReturnsFailure()
    {
        var contactId = Guid.NewGuid();
        var user = User.Create(_tenantId, "kc-1", "u@test.com", "U", "One");
        user.LinkContact(contactId, UserId.From(_actorUserId));
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(
            new LinkUserContactCommand(user.Id.Value, contactId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_user_link_contact_already_linked");
    }

    [Fact]
    public async Task Handle_AlreadyLinked_ToDifferentContact_ReturnsFailure()
    {
        var user = User.Create(_tenantId, "kc-1", "u@test.com", "U", "One");
        user.LinkContact(Guid.NewGuid(), UserId.From(_actorUserId));
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(
            new LinkUserContactCommand(user.Id.Value, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_user_link_contact_already_linked_to_different_contact");
    }

    [Fact]
    public async Task Handle_WhenContactAlreadyLinkedToAnotherUser_ReturnsFailure()
    {
        // Arrange — User A claims contact C first.
        var contactId = Guid.NewGuid();
        var userA = User.Create(_tenantId, "kc-a", "a@test.com", "A", "One");
        userA.LinkContact(contactId, UserId.From(_actorUserId));
        _dbContext.Users.Add(userA);

        var userB = User.Create(_tenantId, "kc-b", "b@test.com", "B", "Two");
        _dbContext.Users.Add(userB);

        await _dbContext.SaveChangesAsync();

        // Act — User B attempts to link to the same contact.
        var result = await CreateHandler().Handle(
            new LinkUserContactCommand(userB.Id.Value, contactId), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_user_link_contact_contact_already_in_use");

        var reloaded = await _dbContext.Users.FindAsync(userB.Id);
        reloaded!.ContactId.Should().BeNull();

        await _outbox.DidNotReceive().EnqueueAsync(
            Arg.Any<UserContactLinkedIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_EmptyIds_Fails()
    {
        var validator = new LinkUserContactValidator();
        var result = validator.Validate(new LinkUserContactCommand(Guid.Empty, Guid.Empty));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "lockey_identity_validation_user_id_required");
        result.Errors.Should().Contain(e => e.ErrorMessage == "lockey_identity_user_link_contact_contact_id_required");
    }
}
