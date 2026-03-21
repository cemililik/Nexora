using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Documents.Domain.Events;

/// <summary>Raised when a document rendering from a template is requested.</summary>
public sealed record DocumentRenderRequestedEvent(DocumentId DocumentId, DocumentTemplateId TemplateId) : DomainEventBase;
