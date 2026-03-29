using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Nexora.Infrastructure.Persistence;

/// <summary>
/// EF Core model cache key factory that includes the tenant schema name.
/// Without this, EF Core caches one model per DbContext type — the first tenant's
/// schema becomes the cached schema for ALL subsequent uses, which is a bug
/// in multi-tenant environments.
/// </summary>
/// <remarks>
/// This factory is DI-free — it reads the schema from BaseDbContext directly,
/// avoiding the need to resolve ITenantContextAccessor from EF Core's internal DI.
/// </remarks>
public sealed class TenantModelCacheKeyFactory : IModelCacheKeyFactory
{
    /// <inheritdoc />
    public object Create(DbContext context, bool designTime)
    {
        var schema = context is BaseDbContext baseCtx
            ? baseCtx.GetCurrentSchema()
            : "default";

        return (context.GetType(), schema, designTime);
    }
}
