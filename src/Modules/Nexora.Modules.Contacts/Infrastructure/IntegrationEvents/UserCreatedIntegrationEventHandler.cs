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
