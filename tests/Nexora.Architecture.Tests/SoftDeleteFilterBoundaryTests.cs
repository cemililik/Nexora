using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Architecture.Tests;

/// <summary>
/// T-022 preventive guard (follows T-021 cleanup): every unique/partial index
/// whose <c>Filter</c> metadata references the <c>"IsDeleted"</c> column must
/// target an entity that actually implements <see cref="ISoftDeletable"/>.
/// Otherwise the filter is dead code in the best case (a column that doesn't
/// exist on the table — silently dropped on some providers) and a CREATE INDEX
/// failure in the worst case (fresh Postgres bail with 42703).
/// The drift T-021 just fixed (ContactTag + ContactCustomField declared
/// <c>HasFilter("\"IsDeleted\" = false")</c> on <see cref="Entity{T}"/>
/// descendants) must not re-enter the codebase via a future copy-paste.
/// </summary>
public sealed class SoftDeleteFilterBoundaryTests
{
    private static readonly (string ContextTypeName, Type ContextType)[] ModuleDbContexts =
    [
        ("ContactsDbContext", typeof(Nexora.Modules.Contacts.Infrastructure.ContactsDbContext)),
        ("DocumentsDbContext", typeof(Nexora.Modules.Documents.Infrastructure.DocumentsDbContext)),
        ("NotificationsDbContext", typeof(Nexora.Modules.Notifications.Infrastructure.NotificationsDbContext)),
        ("ReportingDbContext", typeof(Nexora.Modules.Reporting.Infrastructure.ReportingDbContext)),
        ("AuditDbContext", typeof(Nexora.Modules.Audit.Infrastructure.AuditDbContext)),
        ("IdentityDbContext", typeof(Nexora.Modules.Identity.Infrastructure.IdentityDbContext)),
        ("PlatformDbContext", typeof(Nexora.Modules.Identity.Infrastructure.PlatformDbContext)),
    ];

    [Fact]
    public void HasFilter_ReferencingIsDeleted_ShouldOnlyTargetSoftDeletableEntities()
    {
        var offenders = new List<string>();

        foreach (var (contextName, contextType) in ModuleDbContexts)
        {
            foreach (var offender in ScanContext(contextName, contextType))
                offenders.Add(offender);
        }

        offenders.Should().BeEmpty(
            "every unique index that filters on \"IsDeleted\" must be declared on an ISoftDeletable entity — otherwise the column does not exist on the table and the filter is dead code (see T-021 for the last time this drifted). Offending configurations:\n" +
            string.Join("\n", offenders));
    }

    [Fact]
    public void Guard_Itself_CatchesContrivedDrift()
    {
        // Self-test: a fake model with a non-soft-deletable entity carrying an
        // IsDeleted filter must be caught by the scanner. Locks the guard against
        // silently going no-op if the reflection path breaks.
        var options = new DbContextOptionsBuilder<DriftProbeContext>()
            .UseInMemoryDatabase($"drift-{Guid.NewGuid()}")
            .Options;
        using var ctx = new DriftProbeContext(options);

        var offenders = ScanModel("DriftProbeContext", ctx.Model).ToList();

        offenders.Should().ContainSingle(o => o.Contains("NonSoftDeletableWithFilter"),
            "the contrived drift entity is exactly the shape this guard must catch; a silent pass indicates the scanner logic is broken.");
    }

    // --- Scanner ---------------------------------------------------------------

    private static IEnumerable<string> ScanContext(string contextName, Type contextType)
    {
        var optionsBuilderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType);
        dynamic builder = Activator.CreateInstance(optionsBuilderType)!;
        // T-022 scan is provider-agnostic in intent, but Npgsql index-filter parsing
        // is what we care about catching in production; use the Npgsql provider so
        // the model we inspect is the same one EF would build in prod.
        builder = Microsoft.EntityFrameworkCore.NpgsqlDbContextOptionsBuilderExtensions.UseNpgsql(
            (DbContextOptionsBuilder)builder,
            "Host=localhost;Database=drift-probe;Username=x;Password=x");
        var options = (DbContextOptions)builder.Options;

        // Every module DbContext follows one of two ctors:
        //   (options, ITenantContextAccessor)
        //   (options, ITenantContextAccessor, DomainEventDispatcher?)
        // We probe ctors in decreasing arity and supply minimal arguments.
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(Guid.NewGuid().ToString());

        DbContext? ctx = null;
        foreach (var ctor in contextType
            .GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length))
        {
            var args = ctor.GetParameters()
                .Select(p => ResolveCtorArg(p.ParameterType, options, accessor))
                .ToArray();
            try
            {
                ctx = (DbContext)ctor.Invoke(args);
                break;
            }
            catch
            {
                // Next ctor.
            }
        }

        if (ctx is null)
        {
            yield return $"{contextName}: could not construct DbContext for model inspection (no compatible ctor)";
            yield break;
        }

        using (ctx)
        {
            foreach (var offender in ScanModel(contextName, ctx.Model))
                yield return offender;
        }
    }

    private static object? ResolveCtorArg(Type t, DbContextOptions options, ITenantContextAccessor accessor)
    {
        if (typeof(DbContextOptions).IsAssignableFrom(t)) return options;
        if (t == typeof(ITenantContextAccessor)) return accessor;
        // DomainEventDispatcher and any other optional deps: pass null.
        return null;
    }

    /// <summary>
    /// Walks every declared index on every entity in the model, looks for filter
    /// predicates mentioning the <c>IsDeleted</c> column, and flags any whose
    /// entity type does not implement <see cref="ISoftDeletable"/>.
    /// </summary>
    private static IEnumerable<string> ScanModel(string contextName, Microsoft.EntityFrameworkCore.Metadata.IModel model)
    {
        foreach (var entity in model.GetEntityTypes())
        {
            var entityImplementsSoftDelete = typeof(ISoftDeletable).IsAssignableFrom(entity.ClrType);

            foreach (var index in entity.GetIndexes())
            {
                var filter = index.GetFilter();
                if (filter is null) continue;

                // Case-insensitive match keeps the check provider-agnostic — it
                // fires on `"IsDeleted"`, `isdeleted`, or whatever a future config
                // might write.
                if (!filter.Contains("IsDeleted", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!entityImplementsSoftDelete)
                {
                    yield return $"{contextName} :: {entity.ClrType.Name} — filter '{filter}' references IsDeleted but entity does not implement ISoftDeletable";
                }
            }
        }
    }

    // --- Self-test probe ------------------------------------------------------

    /// <summary>
    /// Contrived context used only by <see cref="Guard_Itself_CatchesContrivedDrift"/>.
    /// The single entity type deliberately mirrors the T-021 drift shape.
    /// </summary>
    private sealed class DriftProbeContext(DbContextOptions<DriftProbeContext> options) : DbContext(options)
    {
        public DbSet<NonSoftDeletableWithFilter> Rows => Set<NonSoftDeletableWithFilter>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NonSoftDeletableWithFilter>(e =>
            {
                e.ToTable("drift_probe");
                e.HasKey(r => r.Id);
                // This is the exact drift the scanner must catch.
                e.HasIndex(r => r.Slug).IsUnique().HasFilter("\"IsDeleted\" = false");
            });
        }
    }

    private sealed class NonSoftDeletableWithFilter
    {
        public Guid Id { get; set; }
        public string Slug { get; set; } = "";
    }
}
