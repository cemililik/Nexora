# Architecture Documentation

This directory hosts the architectural reference for Nexora. It is divided into a *big-picture*
layer and several *deep-dive* layers. Start with [`overview.md`](./overview.md), then follow
links into the narrower documents as needed.

## Index

### Big picture
- [`overview.md`](./overview.md) — concise introduction: solution structure, stack, deployment
  topology, and the key architecture rules that every contributor must know.

### Deep dives
- [`MODULE_SYSTEM.md`](./MODULE_SYSTEM.md) — the `IModule` contract, module lifecycle,
  install/uninstall semantics, dependency declaration, and cross-module messaging patterns.
- [`multi-tenancy.md`](./multi-tenancy.md) — schema-per-tenant isolation model: the tenant
  middleware pipeline, schema lifecycle, Keycloak realm mapping, and tenant propagation into
  background jobs.
- [`portal-extensions.md`](./portal-extensions.md) — how the Admin / Portal hosts dynamically
  assemble their UI from per-module manifests: schema, loader mechanics, failure modes.
- [`MANAGEMENT_PORTAL.md`](./MANAGEMENT_PORTAL.md) — Nexora Management Portal (NMP) architecture:
  platform-admin separation, licensing, billing, tenant provisioning.
- [`COMMUNICATION_FLOW.md`](./COMMUNICATION_FLOW.md) — request path: APISIX → Host → modules →
  Dapr → persistence; observability wiring along the path.

> Historical note: the pre-restructure `OVERVIEW.md` has been archived at
> [`../_archive/architecture-legacy/OVERVIEW.md`](../_archive/architecture-legacy/OVERVIEW.md).

## Division of responsibility

| Aspect | Document |
|--------|----------|
| "Where do I start?" | `overview.md` |
| "How do modules plug in on the server?" | `MODULE_SYSTEM.md` |
| "How is tenant data isolated?" | `multi-tenancy.md` |
| "How does the frontend compose modules per tenant?" | `portal-extensions.md` |
| "How does NMP run the platform?" | `MANAGEMENT_PORTAL.md` |
| "What does a request path look like end-to-end?" | `COMMUNICATION_FLOW.md` |

## Related

- ADRs: [`../decisions/`](../decisions/) — every architectural choice summarised here is formally
  recorded in the decisions folder (0001 through 0017 at time of writing).
- Standards: [`../standards/`](../standards/) — conventions, code style, observability, security,
  and documentation rules referenced throughout these docs.
- Modules: [`../modules/`](../modules/) — per-module specs that implement these architectural
  patterns.
