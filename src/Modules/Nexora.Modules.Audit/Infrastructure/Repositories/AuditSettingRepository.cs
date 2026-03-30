using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Domain.Repositories;

namespace Nexora.Modules.Audit.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IAuditSettingRepository"/>.</summary>
public sealed class AuditSettingRepository(AuditDbContext dbContext) : IAuditSettingRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditSetting>> GetAllByTenantAsync(string tenantId, CancellationToken ct)
    {
        return await dbContext.AuditSettings.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.Module)
            .ThenBy(s => s.Operation)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<AuditSetting?> FindByKeyAsync(string tenantId, string module, string operation, CancellationToken ct)
    {
        return await dbContext.AuditSettings
            .FirstOrDefaultAsync(s =>
                s.TenantId == tenantId &&
                s.Module == module &&
                s.Operation == operation,
                ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditSetting>> FindConfigSettingsAsync(string tenantId, string module, string operation, CancellationToken ct)
    {
        return await dbContext.AuditSettings.AsNoTracking()
            .Where(s =>
                s.TenantId == tenantId &&
                (s.Module == module || s.Module == "*") &&
                (s.Operation == operation || s.Operation == "*") &&
                !(s.Module == "*" && s.Operation != "*"))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public void Add(AuditSetting setting)
    {
        dbContext.AuditSettings.Add(setting);
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await dbContext.SaveChangesAsync(ct);
    }
}
