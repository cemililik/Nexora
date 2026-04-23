using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Identity.Domain.Events;

/// <summary>Raised when a user's contact link is removed.</summary>
/// <param name="UserId">The identity of the user being unlinked.</param>
/// <param name="TenantId">Tenant scope.</param>
/// <param name="PreviousContactId">The Contacts module contact id that was previously linked.
/// Stored as a primitive <see cref="Guid"/> because the Contacts <c>ContactId</c> value object
/// is module-internal and Identity must not reference it directly (module boundary).</param>
/// <param name="UnlinkedByUserId">The actor user who performed the unlink, strongly-typed.</param>
public sealed record UserContactUnlinkedDomainEvent(
    UserId UserId,
    TenantId TenantId,
    Guid PreviousContactId,
    UserId UnlinkedByUserId) : DomainEventBase;
