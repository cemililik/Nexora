using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Audit.Application.Commands;
using Nexora.SharedKernel.Abstractions.Audit;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Nexora.Modules.Audit.Tests.Application;

public sealed class RecordAuthEventTests
{
    private readonly IAuditStore _auditStore = Substitute.For<IAuditStore>();
    private readonly IAuditContext _auditContext = Substitute.For<IAuditContext>();
    private readonly ITenantContextAccessor _tenantAccessor = Substitute.For<ITenantContextAccessor>();
    private readonly RecordAuthEventHandler _handler;

    public RecordAuthEventTests()
    {
        _handler = new RecordAuthEventHandler(
            _auditStore, _auditContext, _tenantAccessor,
            NullLogger<RecordAuthEventHandler>.Instance);

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns("tenant-1");
        _tenantAccessor.Current.Returns(tenantContext);

        _auditContext.UserId.Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        _auditContext.UserEmail.Returns("user@example.com");
        _auditContext.IpAddress.Returns("127.0.0.1");
        _auditContext.UserAgent.Returns("TestAgent/1.0");
        _auditContext.CorrelationId.Returns("corr-1");
    }

    [Fact]
    public async Task Handle_Login_persists_audit_entry_with_Auth_prefix_operation()
    {
        AuditEntry? captured = null;
        await _auditStore.SaveAsync(Arg.Do<AuditEntry>(e => captured = e), Arg.Any<CancellationToken>());

        var result = await _handler.Handle(
            new RecordAuthEventCommand(AuthEventType.Login, IsSuccess: true, Metadata: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.Module.Should().Be("identity");
        captured.Operation.Should().Be("Auth.Login");
        captured.OperationType.Should().Be(OperationType.Action);
        captured.EntityType.Should().Be("AuthSession");
        captured.IsSuccess.Should().BeTrue();
        captured.UserId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        captured.EntityId.Should().Be("11111111-1111-1111-1111-111111111111");
        captured.IpAddress.Should().Be("127.0.0.1");
        captured.UserAgent.Should().Be("TestAgent/1.0");
        captured.CorrelationId.Should().Be("corr-1");
        captured.ErrorKey.Should().BeNull();
        captured.Metadata.Should().BeNull();
    }

    [Fact]
    public async Task Handle_LoginFailed_captures_failure_flag()
    {
        AuditEntry? captured = null;
        await _auditStore.SaveAsync(Arg.Do<AuditEntry>(e => captured = e), Arg.Any<CancellationToken>());

        await _handler.Handle(
            new RecordAuthEventCommand(AuthEventType.LoginFailed, IsSuccess: false, Metadata: """{"reason":"bad_password"}"""),
            CancellationToken.None);

        captured!.IsSuccess.Should().BeFalse();
        captured.Operation.Should().Be("Auth.LoginFailed");
        captured.Metadata.Should().Contain("bad_password");
    }

    [Fact]
    public async Task Handle_without_tenant_context_uses_empty_tenant_slot()
    {
        _tenantAccessor.Current.Throws<InvalidOperationException>();

        AuditEntry? captured = null;
        await _auditStore.SaveAsync(Arg.Do<AuditEntry>(e => captured = e), Arg.Any<CancellationToken>());

        var result = await _handler.Handle(
            new RecordAuthEventCommand(AuthEventType.Logout, IsSuccess: true, Metadata: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured!.TenantId.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_store_failure_returns_failure_result_not_exception()
    {
        _auditStore
            .SaveAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("store down"));

        var result = await _handler.Handle(
            new RecordAuthEventCommand(AuthEventType.TokenRefresh, IsSuccess: true, Metadata: null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_audit_error_auth_event_failed");
    }
}
