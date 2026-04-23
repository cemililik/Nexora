using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Identity.Domain.Events;

/// <summary>Raised when a user is linked to a contact record.</summary>
/// <param name="UserId">The identity of the user being linked.</param>
/// <param name="TenantId">Tenant scope.</param>
/// <param name="ContactId">The Contacts module contact id. Stored as a primitive <see cref="Guid"/>
/// because the Contacts <c>ContactId</c> value object is module-internal and Identity
/// must not reference it directly (module boundary).</param>
/// <param name="LinkedByUserId">The actor user who performed the link, strongly-typed.</param>
public sealed record UserContactLinkedDomainEvent(
    UserId UserId,
    TenantId TenantId,
    Guid ContactId,
    UserId LinkedByUserId) : DomainEventBase;
