namespace Nexora.SharedKernel.Domain.Events;

/// <summary>Published when a contact export job completes successfully and its file is available.</summary>
public sealed record ContactExportCompletedIntegrationEvent : IntegrationEventBase
{
    /// <summary>Gets the export job identifier.</summary>
    public required Guid JobId { get; init; }

    /// <summary>Gets the total number of contact rows written to the export file.</summary>
    public required int TotalRows { get; init; }

    /// <summary>Gets the output file format (csv | xlsx | vcard).</summary>
    public required string Format { get; init; }

    /// <summary>Gets the MinIO storage key for the generated file.</summary>
    public required string StorageKey { get; init; }

    /// <summary>Gets the user that triggered the export, when available.</summary>
    public required Guid? TriggeredByUserId { get; init; }

    /// <summary>
    /// Gets the organization id that scoped the export (usually the caller's
    /// current org). Downstream consumers (e.g. the Notifications
    /// `ContactExportCompletedNotificationHandler`) use this to scope the
    /// in-app notification to the right organization so template resolution
    /// and per-org preferences apply. <c>null</c> when the export was issued
    /// at the tenant level (no org context).
    /// </summary>
    public Guid? OrganizationId { get; init; }

    /// <summary>Gets the UTC timestamp when the export completed.</summary>
    public required DateTime CompletedAtUtc { get; init; }
}
