using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Audit.Application.Queries;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Domain.Repositories;
using Nexora.Modules.Audit.Domain.ValueObjects;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using NSubstitute;

namespace Nexora.Modules.Audit.Tests.Application;

public sealed class GetAuditLogDetailQueryTests
{
    private readonly IAuditEntryRepository _repository;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly string _tenantId = Guid.NewGuid().ToString();

    public GetAuditLogDetailQueryTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        _repository = Substitute.For<IAuditEntryRepository>();
    }

    [Fact]
    public async Task Handle_ExistingEntry_ShouldReturnFullDetail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        var entry = AuditEntry.Create(
            _tenantId, "Contacts", "CreateContact", "Command",
            userId, "user@test.com", "192.168.1.1", "Mozilla/5.0",
            "corr-123", true, null, "Contact", "entity-1",
            "{\"name\":null}", "{\"name\":\"John\"}", "{\"name\":[null,\"John\"]}",
            "{\"source\":\"api\"}", timestamp);

        _repository.GetByIdAsync(entry.Id, _tenantId, Arg.Any<CancellationToken>())
            .Returns(entry);

        var handler = new GetAuditLogDetailHandler(_repository, _tenantAccessor,
            NullLogger<GetAuditLogDetailHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new GetAuditLogDetailQuery(entry.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var detail = result.Value!;
        detail.Id.Should().Be(entry.Id.Value);
        detail.Module.Should().Be("Contacts");
        detail.Operation.Should().Be("CreateContact");
        detail.OperationType.Should().Be("Command");
        detail.UserEmail.Should().Be("user@test.com");
        detail.IsSuccess.Should().BeTrue();
        detail.EntityType.Should().Be("Contact");
        detail.EntityId.Should().Be("entity-1");
        detail.Timestamp.Should().Be(timestamp);
        detail.UserId.Should().Be(userId);
        detail.IpAddress.Should().Be("192.168.1.1");
        detail.UserAgent.Should().Be("Mozilla/5.0");
        detail.CorrelationId.Should().Be("corr-123");
        detail.ErrorKey.Should().BeNull();
        detail.BeforeState.Should().Be("{\"name\":null}");
        detail.AfterState.Should().Be("{\"name\":\"John\"}");
        detail.Changes.Should().Be("{\"name\":[null,\"John\"]}");
        detail.Metadata.Should().Be("{\"source\":\"api\"}");
    }

    [Fact]
    public async Task Handle_NonExistentEntry_ShouldReturnFailure()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(AuditEntryId.From(id), _tenantId, Arg.Any<CancellationToken>())
            .Returns((AuditEntry?)null);

        var handler = new GetAuditLogDetailHandler(_repository, _tenantAccessor,
            NullLogger<GetAuditLogDetailHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new GetAuditLogDetailQuery(id), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_audit_error_entry_not_found");
    }

    [Fact]
    public async Task Handle_EntryFromDifferentTenant_ShouldReturnFailure()
    {
        // Arrange — repository filters by tenant, so it returns null for our tenant
        var entry = AuditEntry.Create(
            "other-tenant", "Contacts", "CreateContact", "Command",
            null, null, null, null, null, true, null, null, null,
            null, null, null, null, DateTimeOffset.UtcNow);

        _repository.GetByIdAsync(entry.Id, _tenantId, Arg.Any<CancellationToken>())
            .Returns((AuditEntry?)null);

        var handler = new GetAuditLogDetailHandler(_repository, _tenantAccessor,
            NullLogger<GetAuditLogDetailHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new GetAuditLogDetailQuery(entry.Id.Value), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_audit_error_entry_not_found");
    }

    [Fact]
    public async Task Handle_FailedEntry_ShouldReturnErrorKey()
    {
        // Arrange
        var entry = AuditEntry.Create(
            _tenantId, "Identity", "Login", "Command",
            null, "user@test.com", "10.0.0.1", null, null,
            false, "lockey_identity_error_invalid_credentials",
            null, null, null, null, null, null, DateTimeOffset.UtcNow);

        _repository.GetByIdAsync(entry.Id, _tenantId, Arg.Any<CancellationToken>())
            .Returns(entry);

        var handler = new GetAuditLogDetailHandler(_repository, _tenantAccessor,
            NullLogger<GetAuditLogDetailHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new GetAuditLogDetailQuery(entry.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsSuccess.Should().BeFalse();
        result.Value.ErrorKey.Should().Be("lockey_identity_error_invalid_credentials");
    }

    private static ITenantContextAccessor CreateTenantAccessor(string tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId);
        return accessor;
    }
}
