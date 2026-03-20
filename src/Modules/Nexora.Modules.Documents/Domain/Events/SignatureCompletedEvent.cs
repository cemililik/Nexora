using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Documents.Domain.Events;

/// <summary>Raised when all recipients have signed and the request is completed.</summary>
public sealed record SignatureCompletedEvent(SignatureRequestId RequestId, DocumentId DocumentId) : DomainEventBase;
