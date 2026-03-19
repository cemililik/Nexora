using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Domain.Services;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Queries;

/// <summary>Query to find potential duplicate contacts for a given contact.</summary>
public sealed record GetDuplicateContactsQuery(
    Guid ContactId,
    int Threshold = DuplicateDetectionService.DefaultThreshold) : IQuery<IReadOnlyList<DuplicateContactDto>>;

/// <summary>Finds potential duplicate contacts using scoring algorithm.</summary>
public sealed class GetDuplicateContactsHandler(
    ContactsDbContext dbContext,
    DuplicateDetectionService duplicateDetectionService,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetDuplicateContactsHandler> logger) : IQueryHandler<GetDuplicateContactsQuery, IReadOnlyList<DuplicateContactDto>>
{
    public async Task<Result<IReadOnlyList<DuplicateContactDto>>> Handle(
        GetDuplicateContactsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var contactId = ContactId.From(request.ContactId);

        var source = await dbContext.Contacts.FirstOrDefaultAsync(
            c => c.Id == contactId && c.TenantId == tenantId,
            cancellationToken);

        if (source is null)
        {
            logger.LogDebug("Contact {ContactId} not found", request.ContactId);
            return Result<IReadOnlyList<DuplicateContactDto>>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        // Load candidates: same tenant, active, different ID
        var candidates = await dbContext.Contacts
            .Where(c => c.TenantId == tenantId
                        && c.Id != contactId
                        && c.Status == ContactStatus.Active)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var duplicates = candidates
            .Select(c => new
            {
                Contact = c,
                Score = duplicateDetectionService.CalculateScore(source, c)
            })
            .Where(x => x.Score >= request.Threshold)
            .OrderByDescending(x => x.Score)
            .Select(x => new DuplicateContactDto(
                x.Contact.Id.Value, x.Contact.DisplayName, x.Contact.Email,
                x.Contact.Phone, x.Contact.Type.ToString(), x.Contact.Status.ToString(),
                x.Score))
            .ToList();

        return Result<IReadOnlyList<DuplicateContactDto>>.Success(duplicates);
    }
}
