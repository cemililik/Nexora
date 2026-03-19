using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Queries;

/// <summary>Query to list permissions, optionally filtered by module name.</summary>
public sealed record GetPermissionsQuery(string? Module = null) : IQuery<List<PermissionDto>>;

/// <summary>Returns permissions ordered by module/resource/action, with optional module filter.</summary>
public sealed class GetPermissionsHandler(
    IdentityDbContext dbContext) : IQueryHandler<GetPermissionsQuery, List<PermissionDto>>
{
    public async Task<Result<List<PermissionDto>>> Handle(
        GetPermissionsQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Permissions.AsQueryable();

        if (!string.IsNullOrEmpty(request.Module))
            query = query.Where(p => p.Module == request.Module);

        var permissions = await query
            .OrderBy(p => p.Module).ThenBy(p => p.Resource).ThenBy(p => p.Action)
            .Select(p => new PermissionDto(
                p.Id.Value,
                p.Module,
                p.Resource,
                p.Action,
                p.Key,
                p.Description))
            .ToListAsync(cancellationToken);

        return Result<List<PermissionDto>>.Success(permissions);
    }
}
