using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Domain.ValueObjects;

namespace Nexora.Modules.Audit.Tests.Domain;

public sealed class AuditEntryTests
{
    [Fact]
    public void Create_ValidParameters_ShouldReturnAuditEntry()
    {
        var userId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        var entry = AuditEntry.Create(
            tenantId: "tenant-1",
            module: "Contacts",
            operation: "CreateContact",
            operationType: "Command",
            userId: userId,
            userEmail: "user@test.com",
            ipAddress: "192.168.1.1",
            userAgent: "Mozilla/5.0",
            correlationId: "corr-123",
            isSuccess: true,
            errorKey: null,
            entityType: "Contact",
            entityId: "entity-1",
            beforeState: null,
            afterState: "{\"name\":\"John\"}",
            changes: "{\"name\":[null,\"John\"]}",
            metadata: "{\"source\":\"api\"}",
            timestamp: timestamp);

        entry.Should().NotBeNull();
        entry.Id.Value.Should().NotBeEmpty();
        entry.TenantId.Should().Be("tenant-1");
        entry.Module.Should().Be("Contacts");
        entry.Operation.Should().Be("CreateContact");
        entry.OperationType.Should().Be("Command");
        entry.UserId.Should().Be(userId);
        entry.UserEmail.Should().Be("user@test.com");
        entry.IpAddress.Should().Be("192.168.1.1");
        entry.UserAgent.Should().Be("Mozilla/5.0");
        entry.CorrelationId.Should().Be("corr-123");
        entry.IsSuccess.Should().BeTrue();
        entry.ErrorKey.Should().BeNull();
        entry.EntityType.Should().Be("Contact");
        entry.EntityId.Should().Be("entity-1");
        entry.BeforeState.Should().BeNull();
        entry.AfterState.Should().Be("{\"name\":\"John\"}");
        entry.Changes.Should().Be("{\"name\":[null,\"John\"]}");
        entry.Metadata.Should().Be("{\"source\":\"api\"}");
        entry.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void Create_WithNullOptionalFields_ShouldReturnAuditEntry()
    {
        var entry = AuditEntry.Create(
            tenantId: "tenant-1",
            module: "Identity",
            operation: "Login",
            operationType: "Query",
            userId: null,
            userEmail: null,
            ipAddress: null,
            userAgent: null,
            correlationId: null,
            isSuccess: false,
            errorKey: "lockey_identity_error_invalid_credentials",
            entityType: null,
            entityId: null,
            beforeState: null,
            afterState: null,
            changes: null,
            metadata: null,
            timestamp: DateTimeOffset.UtcNow);

        entry.UserId.Should().BeNull();
        entry.UserEmail.Should().BeNull();
        entry.IpAddress.Should().BeNull();
        entry.IsSuccess.Should().BeFalse();
        entry.ErrorKey.Should().Be("lockey_identity_error_invalid_credentials");
    }

    [Fact]
    public void Create_ShouldGenerateUniqueIds()
    {
        var entry1 = AuditEntry.Create(
            "t", "m", "o", "Command", null, null, null, null, null,
            true, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);

        var entry2 = AuditEntry.Create(
            "t", "m", "o", "Command", null, null, null, null, null,
            true, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);

        entry1.Id.Should().NotBe(entry2.Id);
    }

    [Fact]
    public void Create_FailedOperation_ShouldPreserveErrorKey()
    {
        var entry = AuditEntry.Create(
            tenantId: "tenant-1",
            module: "Contacts",
            operation: "DeleteContact",
            operationType: "Command",
            userId: Guid.NewGuid(),
            userEmail: "admin@test.com",
            ipAddress: "10.0.0.1",
            userAgent: null,
            correlationId: "corr-456",
            isSuccess: false,
            errorKey: "lockey_contacts_error_not_found",
            entityType: "Contact",
            entityId: "missing-id",
            beforeState: null,
            afterState: null,
            changes: null,
            metadata: null,
            timestamp: DateTimeOffset.UtcNow);

        entry.IsSuccess.Should().BeFalse();
        entry.ErrorKey.Should().Be("lockey_contacts_error_not_found");
    }
}
