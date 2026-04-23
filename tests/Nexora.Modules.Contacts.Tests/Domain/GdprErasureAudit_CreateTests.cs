using FluentAssertions;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Contacts.Tests.Domain;

public sealed class GdprErasureAudit_CreateTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Contact = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private const string Json = "{\"addresses\":0}";

    [Fact]
    public void Create_NullReason_ThrowsDomainException()
    {
        Action act = () => GdprErasureAudit.Create(Tenant, Contact, User, null!, "anonymized", Json);
        act.Should().Throw<DomainException>()
            .WithMessage("lockey_contacts_error_gdpr_audit_reason_required");
    }

    [Fact]
    public void Create_ReasonTooLong_ThrowsDomainException()
    {
        var longReason = new string('a', 501);
        Action act = () => GdprErasureAudit.Create(Tenant, Contact, User, longReason, "anonymized", Json);
        act.Should().Throw<DomainException>()
            .WithMessage("lockey_contacts_error_gdpr_audit_reason_too_long");
    }

    [Fact]
    public void Create_EmptyTenantId_ThrowsDomainException()
    {
        Action act = () => GdprErasureAudit.Create(Guid.Empty, Contact, User, "valid", "anonymized", Json);
        act.Should().Throw<DomainException>()
            .WithMessage("lockey_contacts_error_gdpr_audit_tenant_required");
    }

    [Fact]
    public void Create_HappyPath_ReturnsAudit()
    {
        var audit = GdprErasureAudit.Create(Tenant, Contact, User, "subject request", "anonymized", Json);
        audit.Should().NotBeNull();
        audit.TenantId.Should().Be(Tenant);
        audit.ContactId.Should().Be(Contact);
        audit.ErasedByUserId.Should().Be(User);
        audit.Reason.Should().Be("subject request");
        audit.Mode.Should().Be("anonymized");
        audit.ChildCountsJson.Should().Be(Json);
        audit.Id.Value.Should().NotBe(Guid.Empty);
    }
}
