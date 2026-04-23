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

public sealed class UnlinkUserContactCommandTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();
    private readonly TenantId _tenantId = TenantId.New();
    private readonly Guid _actorUserId = Guid.NewGuid();

    public UnlinkUserContactCommandTests()
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(_tenantId.Value.ToString(), userId: _actorUserId.ToString());
        _tenantAccessor = accessor;

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    public void Dispose() => _dbContext.Dispose();

    private UnlinkUserContactHandler CreateHandler() =>
        new(_dbContext, _tenantAccessor, _outbox, NullLogger<UnlinkUserContactHandler>.Instance);

    [Fact]
    public async Task Handle_LinkedUser_UnlinksAndEmitsManualReason()
    {
        var user = User.Create(_tenantId, "kc-1", "u@test.com", "U", "One");
        user.LinkContact(Guid.NewGuid(), UserId.From(_actorUserId));
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(
            new UnlinkUserContactCommand(user.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persisted = await _dbContext.Users.FindAsync(user.Id);
        persisted!.ContactId.Should().BeNull();

        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<UserContactUnlinkedIntegrationEvent>(e => e.UserId == user.Id.Value && e.Reason == "manual"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotLinked_Idempotent_NoOutboxEmit()
    {
        var user = User.Create(_tenantId, "kc-1", "u@test.com", "U", "One");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(
            new UnlinkUserContactCommand(user.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _outbox.DidNotReceive().EnqueueAsync(
            Arg.Any<UserContactUnlinkedIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        var result = await CreateHandler().Handle(
            new UnlinkUserContactCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_user_link_contact_user_not_found");
    }

    [Fact]
    public void Validator_EmptyUserId_Fails()
    {
        var validator = new UnlinkUserContactValidator();
        var result = validator.Validate(new UnlinkUserContactCommand(Guid.Empty));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "lockey_identity_validation_user_id_required");
    }
}
