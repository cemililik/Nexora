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

/// <summary>Query to retrieve consent records for a contact.</summary>
public sealed record GetContactConsentsQuery(Guid ContactId) : IQuery<IReadOnlyList<ConsentRecordDto>>;

/// <summary>Handles consent record retrieval for a contact.</summary>
public sealed class GetContactConsentsHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetContactConsentsHandler> logger) : IQueryHandler<GetContactConsentsQuery, IReadOnlyList<ConsentRecordDto>>
{
    public async Task<Result<IReadOnlyList<ConsentRecordDto>>> Handle(
        GetContactConsentsQuery request,
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
            return Result<IReadOnlyList<ConsentRecordDto>>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var records = await dbContext.ConsentRecords
            .Where(c => c.ContactId == contactId)
            .AsNoTracking()
            .OrderByDescending(c => c.GrantedAt)
            .Select(c => new ConsentRecordDto(
                c.Id.Value, c.ContactId.Value, c.ConsentType.ToString(),
                c.Granted, c.Source, c.GrantedAt, c.RevokedAt))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ConsentRecordDto>>.Success(records);
    }
}
