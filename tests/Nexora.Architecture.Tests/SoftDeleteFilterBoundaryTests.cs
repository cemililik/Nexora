using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
    /// <summary>
    /// Discovers every concrete <see cref="DbContext"/> across module
    /// assemblies via reflection. A new module (e.g. <c>BillingDbContext</c>
    /// shipped with the Phase-2 Subscription module) gets covered
    /// automatically — there is no hardcoded list to forget to update.
    /// The seed assemblies are the same project refs the architecture test
    /// project already has, so no extra wiring is needed.
    /// </summary>
    private static IEnumerable<(string ContextTypeName, Type ContextType)> ModuleDbContexts()
    {
        // Touch one type from each module so its assembly is guaranteed to be
        // loaded into the AppDomain before we enumerate. Without this, a module
        // whose types are not yet referenced would be invisible to
        // AppDomain.CurrentDomain.GetAssemblies() and its DbContexts would
        // silently escape the scan. The discard pattern keeps the line short
        // and signals "force-load only, value discarded".
        _ = typeof(Nexora.Modules.Contacts.ContactsModule).Assembly;
        _ = typeof(Nexora.Modules.Documents.DocumentsModule).Assembly;
        _ = typeof(Nexora.Modules.Notifications.NotificationsModule).Assembly;
        _ = typeof(Nexora.Modules.Reporting.ReportingModule).Assembly;
        _ = typeof(Nexora.Modules.Audit.AuditModule).Assembly;
        _ = typeof(Nexora.Modules.Identity.IdentityModule).Assembly;

        // Runtime discovery: every currently-loaded assembly whose name starts
        // with `Nexora.Modules.` participates. A new Phase-2 module drops in
        // automatically once it is referenced anywhere in the test graph — no
        // hardcoded list to forget.
        var moduleAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name is { } n &&
                        n.StartsWith("Nexora.Modules.", StringComparison.Ordinal));

        foreach (var assembly in moduleAssemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract) continue;
                if (!typeof(DbContext).IsAssignableFrom(type)) continue;
                yield return (type.Name, type);
            }
        }
    }

    [Fact]
    public void HasFilter_ReferencingIsDeleted_ShouldOnlyTargetSoftDeletableEntities()
    {
        var offenders = new List<string>();

        foreach (var (contextName, contextType) in ModuleDbContexts())
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
        // T-022 scan is provider-agnostic — index filters live on the model
        // metadata that is set in OnModelCreating, independent of the storage
        // provider. Use the InMemory provider so the scanner runs without a
        // Postgres dependency in CI; the metadata we read
        // (<c>IIndex.GetFilter()</c>) is the same shape EF records for the
        // Npgsql provider in production.
        builder = InMemoryDbContextOptionsExtensions.UseInMemoryDatabase(
            (DbContextOptionsBuilder)builder,
            $"drift-probe-{Guid.NewGuid()}");
        var options = (DbContextOptions)builder.Options;

        // Every module DbContext follows one of two ctors:
        //   (options, ITenantContextAccessor)
        //   (options, ITenantContextAccessor, DomainEventDispatcher?)
        // We probe ctors in decreasing arity and supply minimal arguments.
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(Guid.NewGuid().ToString());

        DbContext? ctx = null;
        var ctorErrors = new List<string>();
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
            // Capture every attempted ctor's failure so a "could not construct"
            // result can name the actual exception(s) instead of swallowing
            // them and producing a generic message that hides infrastructure
            // bugs as if they were filter-drift findings.
            catch (Exception ex)
            {
                var inner = ex is System.Reflection.TargetInvocationException tie ? tie.InnerException ?? ex : ex;
                ctorErrors.Add(
                    $"  ctor({string.Join(", ", ctor.GetParameters().Select(p => p.ParameterType.Name))}) → {inner.GetType().Name}: {inner.Message}");
            }
        }

        if (ctx is null)
        {
            // Distinct prefix ("CTOR-FAILURE") so the test failure message
            // separates "could not even construct DbContext" from
            // "constructed fine, found a HasFilter drift". The two failure
            // classes need different remediation.
            yield return $"CTOR-FAILURE :: {contextName}: no compatible constructor succeeded.\n" +
                         string.Join("\n", ctorErrors);
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
