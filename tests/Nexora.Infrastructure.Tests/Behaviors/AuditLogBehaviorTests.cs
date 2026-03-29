using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Infrastructure.Behaviors;
using Nexora.SharedKernel.Abstractions.Audit;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Results;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Nexora.Infrastructure.Tests.Behaviors;

// --- Test doubles ---

public sealed record TestAuditCommand(string Name) : ICommand<string>;

public sealed record TestAuditQuery(int Id) : IQuery<string>;

public sealed record TestPlainRequest(int Id) : IRequest<string>;

public sealed record TestAuditableCommand(string Name) : ICommand<string>, IAuditable
{
    public string AuditModule => "CustomModule";
    public string AuditOperation => "CustomOp";
    public string? AuditEntityType => "CustomEntity";
}

public sealed class AuditLogBehaviorTests
{
    private readonly IAuditContext _auditContext = Substitute.For<IAuditContext>();
    private readonly IAuditConfigService _configService = Substitute.For<IAuditConfigService>();
    private readonly IAuditStore _auditStore = Substitute.For<IAuditStore>();
    private readonly ITenantContextAccessor _tenantAccessor = Substitute.For<ITenantContextAccessor>();

    private AuditLogBehavior<TRequest, TResponse> CreateBehavior<TRequest, TResponse>()
        where TRequest : IRequest<TResponse>
    {
        var logger = Substitute.For<ILogger<AuditLogBehavior<TRequest, TResponse>>>();
        return new AuditLogBehavior<TRequest, TResponse>(
            _auditContext, _configService, _auditStore, _tenantAccessor, logger);
    }

    private void SetupTenantContext(string tenantId = "tenant-1")
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        _tenantAccessor.Current.Returns(tenantContext);
    }

    private void SetupAuditContext()
    {
        _auditContext.UserId.Returns(Guid.NewGuid());
        _auditContext.UserEmail.Returns("test@example.com");
        _auditContext.IpAddress.Returns("127.0.0.1");
        _auditContext.UserAgent.Returns("TestAgent");
        _auditContext.CorrelationId.Returns("corr-123");
    }

    [Fact]
    public async Task Handle_WhenRequestIsCommand_AuditsExecution()
    {
        // Arrange
        SetupTenantContext();
        SetupAuditContext();
        _configService.IsEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(true);

        var behavior = CreateBehavior<TestAuditCommand, Result<string>>();
        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result<string>.Success("ok"));

        // Act
        var result = await behavior.Handle(new TestAuditCommand("test"), next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _auditStore.Received(1).SaveAsync(
            Arg.Is<AuditEntry>(e => e.IsSuccess && e.OperationType == OperationType.Action),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequestIsQuery_AndEnabled_AuditsExecution()
    {
        // Arrange
        SetupTenantContext();
        SetupAuditContext();
        _configService.IsEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(true);

        var behavior = CreateBehavior<TestAuditQuery, Result<string>>();
        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result<string>.Success("data"));

        // Act
        var result = await behavior.Handle(new TestAuditQuery(1), next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _auditStore.Received(1).SaveAsync(
            Arg.Is<AuditEntry>(e => e.IsSuccess && e.OperationType == OperationType.Read),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequestIsQuery_AndDisabled_SkipsAudit()
    {
        // Arrange
        SetupTenantContext();
        _configService.IsEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(false);

        var behavior = CreateBehavior<TestAuditQuery, Result<string>>();
        var nextCalled = false;
        RequestHandlerDelegate<Result<string>> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result<string>.Success("data"));
        };

        // Act
        await behavior.Handle(new TestAuditQuery(1), next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
        await _auditStore.DidNotReceive().SaveAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenConfigCheckFails_SkipsAuditAndContinues()
    {
        // Arrange
        _configService.IsEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Throws(new InvalidOperationException("Dapr unavailable"));

        var behavior = CreateBehavior<TestAuditCommand, Result<string>>();
        var nextCalled = false;
        RequestHandlerDelegate<Result<string>> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result<string>.Success("ok"));
        };

        // Act
        var result = await behavior.Handle(new TestAuditCommand("test"), next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        await _auditStore.DidNotReceive().SaveAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenHandlerThrows_StillAuditsAsFailure()
    {
        // Arrange
        SetupTenantContext();
        SetupAuditContext();
        _configService.IsEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(true);

        var behavior = CreateBehavior<TestAuditCommand, Result<string>>();
        RequestHandlerDelegate<Result<string>> next = () =>
            throw new InvalidOperationException("Handler exploded");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(new TestAuditCommand("test"), next, CancellationToken.None));

        await _auditStore.Received(1).SaveAsync(
            Arg.Is<AuditEntry>(e => !e.IsSuccess && e.ErrorKey == "lockey_audit_handler_exception"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenHandlerThrows_RethrowsException()
    {
        // Arrange
        SetupTenantContext();
        SetupAuditContext();
        _configService.IsEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(true);

        var behavior = CreateBehavior<TestAuditCommand, Result<string>>();
        var expectedException = new InvalidOperationException("Handler exploded");
        RequestHandlerDelegate<Result<string>> next = () => throw expectedException;

        // Act
        var act = () => behavior.Handle(new TestAuditCommand("test"), next, CancellationToken.None);

        // Assert
        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.WithMessage("Handler exploded");
    }

    [Fact]
    public async Task Handle_WhenAuditSaveFails_ContinuesNormally()
    {
        // Arrange
        SetupTenantContext();
        SetupAuditContext();
        _configService.IsEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(true);
        _auditStore.SaveAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("Database unavailable"));

        var behavior = CreateBehavior<TestAuditCommand, Result<string>>();
        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result<string>.Success("ok"));

        // Act
        var result = await behavior.Handle(new TestAuditCommand("test"), next, CancellationToken.None);

        // Assert — business result returned despite audit save failure
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_WhenRequestIsOther_SkipsAuditEntirely()
    {
        // Arrange — plain IRequest, not ICommand or IQuery
        var behavior = CreateBehavior<TestPlainRequest, string>();
        var nextCalled = false;
        RequestHandlerDelegate<string> next = () =>
        {
            nextCalled = true;
            return Task.FromResult("result");
        };

        // Act
        var result = await behavior.Handle(new TestPlainRequest(1), next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
        result.Should().Be("result");
        await _configService.DidNotReceive().IsEnabledAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>());
        await _auditStore.DidNotReceive().SaveAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequestIsAuditable_UsesCustomModuleAndOperation()
    {
        // Arrange
        SetupTenantContext();
        SetupAuditContext();
        _configService.IsEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(true);

        var behavior = CreateBehavior<TestAuditableCommand, Result<string>>();
        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result<string>.Success("ok"));

        // Act
        await behavior.Handle(new TestAuditableCommand("test"), next, CancellationToken.None);

        // Assert
        await _auditStore.Received(1).SaveAsync(
            Arg.Is<AuditEntry>(e =>
                e.Module == "CustomModule" &&
                e.Operation == "CustomOp" &&
                e.EntityType == "CustomEntity"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenResultIsFailure_AuditsAsFailure()
    {
        // Arrange
        SetupTenantContext();
        SetupAuditContext();
        _configService.IsEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(true);

        var behavior = CreateBehavior<TestAuditCommand, Result<string>>();
        RequestHandlerDelegate<Result<string>> next = () =>
            Task.FromResult(Result<string>.Failure("lockey_some_error"));

        // Act
        await behavior.Handle(new TestAuditCommand("test"), next, CancellationToken.None);

        // Assert
        await _auditStore.Received(1).SaveAsync(
            Arg.Is<AuditEntry>(e => !e.IsSuccess && e.ErrorKey == "lockey_some_error"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CommandDefaultEnabled_PassesTrueToConfigService()
    {
        // Arrange
        SetupTenantContext();
        SetupAuditContext();
        _configService.IsEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(true);

        var behavior = CreateBehavior<TestAuditCommand, Result<string>>();
        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result<string>.Success("ok"));

        // Act
        await behavior.Handle(new TestAuditCommand("test"), next, CancellationToken.None);

        // Assert — commands pass defaultEnabled=true
        await _configService.Received(1).IsEnabledAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), true);
    }

    [Fact]
    public async Task Handle_QueryDefaultDisabled_PassesFalseToConfigService()
    {
        // Arrange
        SetupTenantContext();
        SetupAuditContext();
        _configService.IsEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(false);

        var behavior = CreateBehavior<TestAuditQuery, Result<string>>();
        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result<string>.Success("data"));

        // Act
        await behavior.Handle(new TestAuditQuery(1), next, CancellationToken.None);

        // Assert — queries pass defaultEnabled=false
        await _configService.Received(1).IsEnabledAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), false);
    }

    [Fact]
    public async Task Handle_NonModuleNamespace_ExtractsUnknownModule()
    {
        // Arrange — TestAuditCommand is in Nexora.Infrastructure.Tests.Behaviors, not Nexora.Modules.*
        SetupTenantContext();
        SetupAuditContext();
        _configService.IsEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(true);

        var behavior = CreateBehavior<TestAuditCommand, Result<string>>();
        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result<string>.Success("ok"));

        // Act
        await behavior.Handle(new TestAuditCommand("test"), next, CancellationToken.None);

        // Assert — module should be "Unknown" for non-Nexora.Modules namespace
        await _auditStore.Received(1).SaveAsync(
            Arg.Is<AuditEntry>(e => e.Module == "Unknown"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CommandWithCommandSuffix_StripsCommandFromOperation()
    {
        // Arrange — TestAuditCommand name ends with "Command" implicitly via the type name
        // The type is "TestAuditCommand" so operation should be "TestAudit" (Command suffix stripped)
        SetupTenantContext();
        SetupAuditContext();
        _configService.IsEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(true);

        var behavior = CreateBehavior<TestAuditCommand, Result<string>>();
        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result<string>.Success("ok"));

        // Act
        await behavior.Handle(new TestAuditCommand("test"), next, CancellationToken.None);

        // Assert — "TestAuditCommand" → operation "TestAudit"
        await _auditStore.Received(1).SaveAsync(
            Arg.Is<AuditEntry>(e => e.Operation == "TestAudit"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_QueryWithQuerySuffix_AddsQueryPrefixToOperation()
    {
        // Arrange — TestAuditQuery name ends with "Query"
        // The type is "TestAuditQuery" so operation should be "Query.TestAudit"
        SetupTenantContext();
        SetupAuditContext();
        _configService.IsEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(true);

        var behavior = CreateBehavior<TestAuditQuery, Result<string>>();
        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result<string>.Success("data"));

        // Act
        await behavior.Handle(new TestAuditQuery(1), next, CancellationToken.None);

        // Assert — "TestAuditQuery" → operation "Query.TestAudit"
        await _auditStore.Received(1).SaveAsync(
            Arg.Is<AuditEntry>(e => e.Operation == "Query.TestAudit"),
            Arg.Any<CancellationToken>());
    }
}
