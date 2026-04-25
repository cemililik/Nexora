using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Api;

/// <summary>
/// T-029: <c>GET /api/v1/identity/demo/scenarios</c>. Returns the
/// scenarios applicable to the current tenant — the admin
/// "Create Demo Environment" dialog (T-008) calls this to populate
/// its dropdown instead of carrying a hardcoded list. Permission gate
/// reuses <c>platform.tenants.create_demo</c> (introduced in T-008) so
/// the same operator-set that can run the seed can list the scenarios.
/// </summary>
public static class DemoScenarioEndpoints
{
    /// <summary>Maps the scenario-listing endpoint under the existing identity group.</summary>
    public static void MapDemoScenarioEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/demo")
            .RequireAuthorization();

        group.MapGet("/scenarios", async (
            IDemoScenarioRegistry registry,
            ITenantContextAccessor tenantAccessor,
            CancellationToken ct) =>
        {
            // The current tenant comes from the JWT-derived ambient
            // accessor — same as every other tenant-scoped endpoint.
            // Admin-portal callers always carry a tenant id even when
            // operating as a platform user; absence is an auth-config bug
            // and surfaces here as a 400 (preferable to a silent empty
            // list that an operator might mis-read as "no scenarios").
            var tenantId = tenantAccessor.Current.TenantId;
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return Results.BadRequest(ApiEnvelope<List<DemoScenarioDto>>.Fail(
                    new Error(SharedKernel.Localization.LocalizedMessage.Of(
                        "lockey_demo_scenarios_missing_tenant"))));
            }

            var scenarios = await registry.GetForTenantAsync(tenantId, ct);
            var dtos = scenarios
                .Select(s => new DemoScenarioDto(
                    Name: s.Name,
                    DescriptionLockey: s.DescriptionLockey,
                    RequiredModules: s.RequiredModules,
                    OptionalModules: s.OptionalModules))
                .ToList();
            return Results.Ok(ApiEnvelope<List<DemoScenarioDto>>.Success(dtos));
        })
        .RequireAuthorization("platform.tenants.create_demo")
        .WithSummary("List demo scenarios available for the current tenant (platform operators only)")
        .WithDescription(
            "Returns the scenarios whose RequiredModules are all installed for the " +
            "current tenant. Empty list is a legitimate response when no module supports " +
            "any scenario; admin UI should render an EmptyState in that case.")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}

/// <summary>
/// API-shape mirror of <see cref="DemoScenario"/>. Kept module-local so
/// the SharedKernel record stays domain-pure (no API metadata) and the
/// HTTP contract can evolve independently if needed.
/// </summary>
public sealed record DemoScenarioDto(
    string Name,
    string DescriptionLockey,
    IReadOnlyList<string> RequiredModules,
    IReadOnlyList<string> OptionalModules);
