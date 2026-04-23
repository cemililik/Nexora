namespace Nexora.SharedKernel.Domain.Events;

/// <summary>
/// Published by the Identity module when a user↔contact link is removed.
/// Reason distinguishes manual admin action from automated GDPR erasure cascade.
/// </summary>
public sealed record UserContactUnlinkedIntegrationEvent : IntegrationEventBase
{
    /// <summary>The Identity user that was unlinked.</summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// The previously linked Contact identifier. Nullable because the unlink may have fired
    /// from a cleanup path where no prior link existed at the moment of publication.
    /// </summary>
    public Guid? ContactId { get; init; }

    /// <summary>UTC timestamp when the unlink occurred.</summary>
    public required DateTime UnlinkedAtUtc { get; init; }

    /// <summary>Reason for the unlink — one of "manual" | "gdpr_erasure".</summary>
    public required string Reason { get; init; }
}
