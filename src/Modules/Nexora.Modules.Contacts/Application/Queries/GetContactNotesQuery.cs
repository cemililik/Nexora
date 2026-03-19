using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Queries;

/// <summary>Query to retrieve notes for a contact. Pinned notes appear first.</summary>
public sealed record GetContactNotesQuery(Guid ContactId) : IQuery<IReadOnlyList<ContactNoteDto>>;

/// <summary>Handles note retrieval for a contact.</summary>
public sealed class GetContactNotesHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetContactNotesHandler> logger) : IQueryHandler<GetContactNotesQuery, IReadOnlyList<ContactNoteDto>>
{
    public async Task<Result<IReadOnlyList<ContactNoteDto>>> Handle(
        GetContactNotesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var contactId = ContactId.From(request.ContactId);

        var contactExists = await dbContext.Contacts.AnyAsync(
            c => c.Id == contactId && c.TenantId == tenantId,
            cancellationToken);

        if (!contactExists)
        {
            logger.LogDebug("Contact {ContactId} not found", request.ContactId);
            return Result<IReadOnlyList<ContactNoteDto>>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var notes = await dbContext.ContactNotes
            .Where(n => n.ContactId == contactId)
            .AsNoTracking()
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedAt)
            .Select(n => new ContactNoteDto(
                n.Id.Value, n.ContactId.Value, n.AuthorUserId,
                n.Content, n.IsPinned, n.CreatedAt, n.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ContactNoteDto>>.Success(notes);
    }
}
