using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.Events;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Documents.Tests.Domain;

public sealed class SignatureRequestTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly DocumentId _documentId = DocumentId.New();
    private readonly Guid _userId = Guid.NewGuid();

    private SignatureRequest CreateRequest(string title = "Contract Signing") =>
        SignatureRequest.Create(_tenantId, _orgId, _documentId, _userId, title);

    [Fact]
    public void Create_ValidInput_ShouldSetProperties()
    {
        // Arrange & Act
        var request = CreateRequest();

        // Assert
        request.Id.Value.Should().NotBeEmpty();
        request.TenantId.Should().Be(_tenantId);
        request.DocumentId.Should().Be(_documentId);
        request.Title.Should().Be("Contract Signing");
        request.Status.Should().Be(SignatureRequestStatus.Draft);
        request.Recipients.Should().BeEmpty();
    }

    [Fact]
    public void AddRecipient_InDraft_ShouldAddRecipient()
    {
        // Arrange
        var request = CreateRequest();
        var contactId = Guid.NewGuid();

        // Act
        request.AddRecipient(contactId, "john@test.com", "John Doe", 1);

        // Assert
        request.Recipients.Should().ContainSingle();
        request.Recipients[0].Email.Should().Be("john@test.com");
        request.Recipients[0].Name.Should().Be("John Doe");
        request.Recipients[0].SigningOrder.Should().Be(1);
    }

    [Fact]
    public void AddRecipient_AfterSent_ShouldThrow()
    {
        // Arrange
        var request = CreateRequest();
        request.AddRecipient(Guid.NewGuid(), "a@b.com", "Test", 1);
        request.Send();

        // Act
        var act = () => request.AddRecipient(Guid.NewGuid(), "c@d.com", "Late", 2);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Send_WithRecipients_ShouldChangeStatus()
    {
        // Arrange
        var request = CreateRequest();
        request.AddRecipient(Guid.NewGuid(), "a@b.com", "Test", 1);

        // Act
        request.Send();

        // Assert
        request.Status.Should().Be(SignatureRequestStatus.Sent);
    }

    [Fact]
    public void Send_WithRecipients_ShouldRaiseDomainEvent()
    {
        // Arrange
        var request = CreateRequest();
        request.AddRecipient(Guid.NewGuid(), "a@b.com", "Test", 1);

        // Act
        request.Send();

        // Assert
        request.DomainEvents.Should().Contain(e => e is SignatureRequestSentEvent);
    }

    [Fact]
    public void Send_WithoutRecipients_ShouldThrow()
    {
        // Arrange
        var request = CreateRequest();

        // Act
        var act = () => request.Send();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Send_WhenNotDraft_ShouldThrow()
    {
        // Arrange
        var request = CreateRequest();
        request.AddRecipient(Guid.NewGuid(), "a@b.com", "Test", 1);
        request.Send();

        // Act
        var act = () => request.Send();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RecordSignature_ShouldSetPartiallySigned()
    {
        // Arrange
        var request = CreateRequest();
        request.AddRecipient(Guid.NewGuid(), "a@b.com", "Alice", 1);
        request.AddRecipient(Guid.NewGuid(), "b@c.com", "Bob", 2);
        request.Send();
        var recipientId = request.Recipients[0].Id;

        // Act
        request.RecordSignature(recipientId, "sig-data", "127.0.0.1");

        // Assert
        request.Status.Should().Be(SignatureRequestStatus.PartiallySigned);
    }

    [Fact]
    public void RecordSignature_AllSigned_ShouldComplete()
    {
        // Arrange
        var request = CreateRequest();
        request.AddRecipient(Guid.NewGuid(), "a@b.com", "Alice", 1);
        request.Send();
        var recipientId = request.Recipients[0].Id;

        // Act
        request.RecordSignature(recipientId, "sig-data", "127.0.0.1");

        // Assert
        request.Status.Should().Be(SignatureRequestStatus.Completed);
        request.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordSignature_AllSigned_ShouldRaiseCompletedEvent()
    {
        // Arrange
        var request = CreateRequest();
        request.AddRecipient(Guid.NewGuid(), "a@b.com", "Alice", 1);
        request.Send();
        request.ClearDomainEvents();
        var recipientId = request.Recipients[0].Id;

        // Act
        request.RecordSignature(recipientId, "sig-data", "127.0.0.1");

        // Assert
        request.DomainEvents.Should().Contain(e => e is SignatureCompletedEvent);
    }

    [Fact]
    public void Cancel_SentRequest_ShouldChangeStatus()
    {
        // Arrange
        var request = CreateRequest();
        request.AddRecipient(Guid.NewGuid(), "a@b.com", "Test", 1);
        request.Send();

        // Act
        request.Cancel();

        // Assert
        request.Status.Should().Be(SignatureRequestStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenCompleted_ShouldThrow()
    {
        // Arrange
        var request = CreateRequest();
        request.AddRecipient(Guid.NewGuid(), "a@b.com", "Test", 1);
        request.Send();
        request.RecordSignature(request.Recipients[0].Id, "sig", "127.0.0.1");

        // Act
        var act = () => request.Cancel();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Expire_SentRequest_ShouldChangeStatus()
    {
        // Arrange
        var request = CreateRequest();
        request.AddRecipient(Guid.NewGuid(), "a@b.com", "Test", 1);
        request.Send();

        // Act
        request.Expire();

        // Assert
        request.Status.Should().Be(SignatureRequestStatus.Expired);
    }

    [Fact]
    public void Expire_SentRequest_ShouldExpirePendingRecipients()
    {
        // Arrange
        var request = CreateRequest();
        request.AddRecipient(Guid.NewGuid(), "a@b.com", "Test", 1);
        request.Send();

        // Act
        request.Expire();

        // Assert
        request.Recipients[0].Status.Should().Be(SignatureRecipientStatus.Expired);
    }

    [Fact]
    public void RecordSignature_WhenDraft_ShouldThrow()
    {
        // Arrange
        var request = CreateRequest();
        request.AddRecipient(Guid.NewGuid(), "a@b.com", "Alice", 1);
        var recipientId = request.Recipients[0].Id;

        // Act
        var act = () => request.RecordSignature(recipientId, "sig-data", "127.0.0.1");

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RecordSignature_WhenCancelled_ShouldThrow()
    {
        // Arrange
        var request = CreateRequest();
        request.AddRecipient(Guid.NewGuid(), "a@b.com", "Alice", 1);
        request.Send();
        request.Cancel();
        var recipientId = request.Recipients[0].Id;

        // Act
        var act = () => request.RecordSignature(recipientId, "sig-data", "127.0.0.1");

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RecordSignature_WhenExpired_ShouldThrow()
    {
        // Arrange
        var request = SignatureRequest.Create(_tenantId, _orgId, _documentId, _userId, "Expiring",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
        request.AddRecipient(Guid.NewGuid(), "a@b.com", "Alice", 1);
        request.Send();
        var recipientId = request.Recipients[0].Id;

        // Act
        var act = () => request.RecordSignature(recipientId, "sig-data", "127.0.0.1");

        // Assert
        act.Should().Throw<DomainException>();
    }
}
