using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;

/// <summary>
/// Handles UserCreatedIntegrationEvent from the Identity module.
/// Auto-creates a contact record for the new user.
/// </summary>
public sealed class UserCreatedIntegrationEventHandler(
    ContactsDbContext dbContext,
    ILogger<UserCreatedIntegrationEventHandler> logger) : IIntegrationEventHandler<UserCreatedIntegrationEvent>
{
    public async Task HandleAsync(UserCreatedIntegrationEvent @event, CancellationToken ct)
    {
        var tenantId = Guid.Parse(@event.TenantId);

        // Check if a contact with this email already exists for the tenant
        var existingContact = await dbContext.Contacts
            .FirstOrDefaultAsync(
                c => c.TenantId == tenantId && c.Email == @event.Email,
                ct);

        if (existingContact is not null)
        {
            logger.LogDebug(
                "Contact already exists for email {Email} in tenant {TenantId}, skipping auto-create",
                @event.Email, tenantId);
            return;
        }

        var contact = Contact.Create(
            tenantId: tenantId,
            organizationId: Guid.Empty,
            type: ContactType.Individual,
            firstName: null,
            lastName: null,
            companyName: null,
            email: @event.Email,
            phone: null,
            source: ContactSource.Api);

        await dbContext.Contacts.AddAsync(contact, ct);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Auto-created contact {ContactId} for user {UserId} in tenant {TenantId}",
            contact.Id, @event.UserId, tenantId);
    }
}

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

    public async Task HandleAsync(OrganizationCreatedIntegrationEvent @event, CancellationToken ct)
    {
        var tenantId = Guid.Parse(@event.TenantId);

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

        foreach (var (name, category, color) in DefaultTags)
        {
            var tag = Tag.Create(tenantId, name, category, color);
            await dbContext.Tags.AddAsync(tag, ct);
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Created {Count} default tags for tenant {TenantId} on organization {OrganizationId} creation",
            DefaultTags.Length, tenantId, @event.OrganizationId);
    }
}
