using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;

/// <summary>
/// Handles OrganizationCreatedIntegrationEvent from the Identity module.
/// Creates default tags for the tenant when a new organization is created.
/// </summary>
public sealed class OrganizationCreatedIntegrationEventHandler(
    ContactsDbContext dbContext,
    ILogger<OrganizationCreatedIntegrationEventHandler> logger) : IIntegrationEventHandler<OrganizationCreatedIntegrationEvent>
{
    private static readonly (string Name, TagCategory Category, string Color)[] DefaultTags =
    [
        ("Donor", TagCategory.Donor, "#22c55e"),
        ("Volunteer", TagCategory.Volunteer, "#3b82f6"),
        ("Parent", TagCategory.Parent, "#f59e0b"),
        ("Student", TagCategory.Student, "#8b5cf6"),
        ("Staff", TagCategory.Staff, "#ef4444"),
        ("Vendor", TagCategory.Vendor, "#6b7280")
    ];

    /// <summary>
    /// Handles an <see cref="OrganizationCreatedIntegrationEvent"/> by creating default tags for the new tenant.
    /// </summary>
    public async Task HandleAsync(OrganizationCreatedIntegrationEvent @event, CancellationToken ct)
    {
        if (!Guid.TryParse(@event.TenantId, out var tenantId))
        {
            logger.LogError("Invalid TenantId {TenantId} in OrganizationCreatedIntegrationEvent", @event.TenantId);
            return;
        }

        // Only create default tags if no tags exist for this tenant yet
        var existingTagCount = await dbContext.Tags
            .CountAsync(t => t.TenantId == tenantId, ct);

        if (existingTagCount > 0)
        {
            logger.LogDebug(
                "Tenant {TenantId} already has {TagCount} tags, skipping default tag creation",
                tenantId, existingTagCount);
            return;
        }

        var tags = DefaultTags.Select(t => Tag.Create(tenantId, t.Name, t.Category, t.Color)).ToList();
        await dbContext.Tags.AddRangeAsync(tags, ct);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Created {Count} default tags for tenant {TenantId} on organization {OrganizationId} creation",
            DefaultTags.Length, tenantId, @event.OrganizationId);
    }
}
