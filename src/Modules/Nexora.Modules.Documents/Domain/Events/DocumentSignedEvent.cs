using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Documents.Domain.Events;

/// <summary>Raised when a recipient signs a document.</summary>
public sealed record DocumentSignedEvent(SignatureRequestId RequestId, SignatureRecipientId RecipientId) : DomainEventBase;
