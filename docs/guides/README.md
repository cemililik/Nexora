# Guides

How-to guides for integrating with and operating Nexora. Each guide is task-oriented: it assumes
you have already read the relevant architectural reference in [`../architecture/`](../architecture/)
and are now trying to *do* something.

## Index

- [`API_INTEGRATION_GUIDE.md`](./API_INTEGRATION_GUIDE.md) — authenticate against Nexora, call
  module endpoints, and handle the `ApiEnvelope<T>` response shape.
- [`cli.md`](./cli.md) — the Nexora host CLI (`nexora <verb> [args]`): dispatch flow,
  exit codes, `demo:load` reference, locale support, and how to add a new verb.
- **Tenant onboarding** — (TODO) end-to-end walkthrough of provisioning a new tenant, including
  Keycloak realm setup, initial module install, and smoke tests.
- **Module install / uninstall** — (TODO) operator guide for installing or removing a module on
  a live tenant, covering pre-flight checks, migration application, and rollback.
- **Portal extension author guide** — (TODO) step-by-step guide to writing a
  `module.manifest.yaml` and publishing a frontend bundle; companion to
  [`../architecture/portal-extensions.md`](../architecture/portal-extensions.md).

## Related

- ADR-017 — Portal Extension Architecture (drives the extension author guide).
- Standard: `../standards/documentation-style.md` (how these guides are written).
- Runbooks: `../operations/` — operational procedures (Helm install, tenant operations).
