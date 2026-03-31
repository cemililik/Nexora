using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Persistence.Outbox;
using Nexora.SharedKernel.Abstractions.Messaging;

namespace Nexora.Infrastructure.Persistence.Inbox;

/// <summary>
/// Idempotent consumer guard backed by the module's own DbContext.
/// Checks and records processed event IDs in the inbox_messages table,
/// ensuring each integration event is handled exactly once per module.
/// </summary>
/// <typeparam name="TContext">The module DbContext that contains the inbox_messages table.</typeparam>
public sealed class InboxGuard<TContext>(TContext dbContext) : IInboxGuard
    where TContext : DbContext
{
    /// <inheritdoc />
    public async Task<bool> IsAlreadyProcessedAsync(Guid eventId, CancellationToken ct = default)
    {
        var alreadyProcessed = await dbContext.Set<InboxMessage>()
            .AnyAsync(m => m.EventId == eventId, ct);

        if (alreadyProcessed)
            OutboxMetrics.InboxDuplicatesSkipped.Add(1);

        return alreadyProcessed;
    }

    /// <inheritdoc />
    public void MarkAsProcessed(Guid eventId, string eventType)
    {
        dbContext.Set<InboxMessage>().Add(InboxMessage.Create(eventId, eventType));
    }
}
