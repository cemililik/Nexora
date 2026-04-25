using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.Events;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Identity.Tests.Domain;

public sealed class User_LinkContactTests
{
    private readonly TenantId _tenantId = TenantId.New();
    private readonly Guid _contactId = Guid.NewGuid();
    private readonly UserId _actorId = UserId.New();

    [Fact]
    public void LinkContact_SetsContactId_AndRaisesEvent()
    {
        var user = User.Create(_tenantId, "kc-human-1", "u@test.com", "U", "One");
        user.ClearDomainEvents();

        user.LinkContact(_contactId, _actorId);

        user.ContactId.Should().Be(_contactId);
        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserContactLinkedDomainEvent>()
                .Which.ContactId.Should().Be(_contactId);
    }

    [Fact]
    public void LinkContact_AlreadyLinked_ToDifferentContact_Throws()
    {
        var user = User.Create(_tenantId, "kc-human-1", "u@test.com", "U", "One");
        user.LinkContact(_contactId, _actorId);

        var act = () => user.LinkContact(Guid.NewGuid(), _actorId);

        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_identity_user_link_contact_already_linked_to_different_contact");
    }

    [Fact]
    public void LinkContact_AlreadyLinked_ToSameContact_Throws()
    {
        var user = User.Create(_tenantId, "kc-human-1", "u@test.com", "U", "One");
        user.LinkContact(_contactId, _actorId);

        var act = () => user.LinkContact(_contactId, _actorId);

        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_identity_user_link_contact_already_linked");
    }

    [Fact]
    public void LinkContact_SystemAccount_Throws()
    {
        var user = User.Create(_tenantId, "service-account-my-client", "svc@test.com", "Svc", "Acct");

        user.IsSystemAccount.Should().BeTrue();

        var act = () => user.LinkContact(_contactId, _actorId);

        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_identity_user_link_contact_system_account_rejected");
    }

    [Fact]
    public void LinkContact_EmptyContactId_Throws()
    {
        var user = User.Create(_tenantId, "kc-1", "u@test.com", "U", "One");

        var act = () => user.LinkContact(Guid.Empty, _actorId);

        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_identity_user_link_contact_contact_id_required");
    }

    [Fact]
    public void UnlinkContact_NotLinked_IsNoOp()
    {
        var user = User.Create(_tenantId, "kc-1", "u@test.com", "U", "One");
        user.ClearDomainEvents();

        user.UnlinkContact(_actorId);

        user.ContactId.Should().BeNull();
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UnlinkContact_WhenLinked_ClearsAndRaisesEvent()
    {
        var user = User.Create(_tenantId, "kc-1", "u@test.com", "U", "One");
        user.LinkContact(_contactId, _actorId);
        user.ClearDomainEvents();

        user.UnlinkContact(_actorId);

        user.ContactId.Should().BeNull();
        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserContactUnlinkedDomainEvent>()
                .Which.PreviousContactId.Should().Be(_contactId);
    }

    [Fact]
    public void IsSystemAccount_HumanUser_False()
    {
        var user = User.Create(_tenantId, "kc-regular-human", "u@test.com", "U", "One");
        user.IsSystemAccount.Should().BeFalse();
    }
}
