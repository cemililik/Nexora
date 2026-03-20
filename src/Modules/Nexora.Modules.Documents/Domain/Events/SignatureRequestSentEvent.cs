using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Documents.Domain.Events;

/// <summary>Raised when a signature request is sent to recipients.</summary>
public sealed record SignatureRequestSentEvent(SignatureRequestId RequestId, DocumentId DocumentId) : DomainEventBase;
