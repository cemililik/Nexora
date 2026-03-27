using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Queries;

/// <summary>Query to list installed modules for a specific tenant.</summary>
public sealed record GetTenantModulesQuery(Guid TenantId) : IQuery<List<TenantModuleDto>>;

/// <summary>Returns all installed modules for a tenant.</summary>
public sealed class GetTenantModulesHandler(
    PlatformDbContext platformDb) : IQueryHandler<GetTenantModulesQuery, List<TenantModuleDto>>
{
    public async Task<Result<List<TenantModuleDto>>> Handle(
        GetTenantModulesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.From(request.TenantId);

        var modules = await platformDb.TenantModules.AsNoTracking()
            .Where(tm => tm.TenantId == tenantId)
            .OrderBy(tm => tm.ModuleName)
            .Select(tm => new TenantModuleDto(
                tm.Id.Value, tm.ModuleName, tm.IsActive,
                tm.InstalledAt, tm.InstalledBy))
            .ToListAsync(cancellationToken);

        return Result<List<TenantModuleDto>>.Success(modules,
            LocalizedMessage.Of("lockey_identity_modules_listed"));
    }
}
