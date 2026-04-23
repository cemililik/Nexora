# 0023 — NMP billing model — plans, entitlements, invoicing

## Status

Accepted

## Date

2026-04-22

## Context

The Nexora Management Portal (NMP) is the operator-facing platform-billing product. It lives
in a separate codebase (`Nexora.Management`), runs on a different Keycloak realm, and has a
release cadence decoupled from the SaaS runtime. NMP sits between the platform operator and
each tenant: it decides what a tenant is entitled to run — tier (ADR-0016), edition bundles,
marketplace extensions (ADR-0017), and headcount/org/API/storage caps — and it bills the
operator's paying customer on a monthly or annual cycle.

The tenant-side `Subscription` module is a separate concern: it handles the tenant's OWN
downstream customers, lives in tenant schemas, and has its own recurring engine (ADR-0019).
NMP is the third recurring engine in the platform (Subscription tenant-side, Fundraising NGO
recurring donations, NMP platform-side); per ADR-0019 these are deliberately kept as distinct
engines that share the `IPaymentProvider` port from ADR-0018.

The NMP.2 milestone on `phase-NMP-track.md` is billing integration. Before NMP.2 begins, the
data model for plans, entitlements, and invoicing has to be frozen so that (a) NMP can be
built against a stable contract, (b) the SaaS tenant admin license tab can query renewal,
plan, and entitlements with confidence, (c) Stripe and iyzico webhooks have a defined landing
surface, and (d) the in-process `NmpLicenseVerifier` used by the SaaS runtime on module
install / feature gate has a stable response shape from `/internal/license/verify`. On-prem
tenants add a fourth constraint: air-gapped or intermittently-connected installations cannot
rely on synchronous NMP roundtrips and must work from an RSA-signed license key that
phone-home updates daily.

## Decision drivers

- Operator must be able to configure any mix of tier + editions + extensions per plan, with
  per-plan headcount, org, API rate, and storage limits.
- Entitlements must be queryable synchronously during license checks (module install, feature
  gate) with a cached snapshot at the SaaS runtime side — O(1) happy path.
- Plan upgrade / downgrade / cancel must mirror the Subscription module's semantics
  (upgrade: immediate-with-proration; downgrade: end-of-period) so operators and SaaS
  customers use one mental model.
- Billing must support Stripe (global) and iyzico (TR tenants) via the shared
  `IPaymentProvider` port (ADR-0018) — no provider-specific leakage into the NMP domain.
- On-prem tenants must be billable annually via a signed license key and must refresh
  entitlements via a bounded phone-home probe.
- Webhook delivery is at-least-once; replay and audit must be first-class.
- NMP code must not be referenced from `Nexora.Host` or module projects — integration is
  HTTPS-only to preserve independent release cadence.

## Considered options

1. **Option A — Plan + Entitlement + Invoice + WebhookLedger (chosen)**

   Four first-class entities in NMP:
   - `Plan` — operator-side SKU: human name + tier + editions[] + extensions[] +
     limits{users, orgs, api_rpm, storage_gb} + base_price + billing_cycle (monthly |
     annual) + currency.
   - `NmpSubscription` — tenant_id + plan_id + status + trial dates + period dates +
     cancel_at_period_end + payment_provider + provider_subscription_id.
   - `Entitlement` — the flattened, cached projection of a plan for one tenant, refreshed on
     subscription change. Fields: tenant_id + tier + editions[] + extensions[] + limits{} +
     expires_at + grace_until + generation. This is what `NmpLicenseVerifier` returns.
   - `NmpInvoice` + `NmpInvoiceLine` — mirror the tenant Subscription module's invoice shape
     to keep reporting consistent across both engines.
   - `WebhookLedger` — append-only log of raw Stripe / iyzico webhook payloads for replay and
     audit.

   Pros: O(1) license check; clean separation of operator SKU (`Plan`) from tenant state
   (`NmpSubscription`) from runtime check (`Entitlement`); air-gapped support via signed-key
   projection of `Entitlement`; provider-agnostic via ledger + `IPaymentProvider`.

   Cons: more tables; must keep `Entitlement` in sync with `Plan` + `NmpSubscription` via
   explicit recompute on every state change.

2. **Option B — Entitlement computed on-the-fly per verify call (rejected)**

   Drop the `Entitlement` cache table; compute the flattened result by joining `Plan` and
   `NmpSubscription` on every `/internal/license/verify` call.

   Pros: simpler schema; no cache-invalidation bug class.

   Cons: every license check roundtrips to NMP and touches two tables; cannot support on-prem
   air-gapped tenants (no `Entitlement` row to sign into the license key); creates tight
   runtime coupling between SaaS and NMP availability.

3. **Option C — Reuse the tenant Subscription module code in NMP (rejected)**

   Appears DRY — collapse tenant-facing and platform-facing recurring billing into one code
   path.

   Cons: collapses two products with different release cadence, different Keycloak realm,
   different failure modes, and different entity spaces (tenant's customers vs. operator's
   customers). Rejected for the same reason ADR-0019 rejected one-engine-fits-all: the
   overlap is coincidence, not design.

## Decision outcome

Adopt **Option A**. Define `Plan`, `NmpSubscription`, `Entitlement`, `NmpInvoice`,
`NmpInvoiceLine`, and `WebhookLedger` as the frozen NMP billing model. The SaaS runtime
interacts with NMP exclusively via `POST /internal/license/verify`, which returns the
`Entitlement` projection. Entitlement recompute is triggered deterministically on every
subscription or plan state change. Stripe and iyzico are handled behind `IPaymentProvider`
(ADR-0018), with webhook dedup via `WebhookLedger`.

### ER diagram

```mermaid
erDiagram
    Plan ||--o{ NmpSubscription : "chosen by"
    NmpSubscription ||--|| Entitlement : "projects to"
    NmpSubscription ||--o{ NmpInvoice : "generates"
    NmpInvoice ||--|{ NmpInvoiceLine : "contains"
    NmpSubscription ||--o{ WebhookLedger : "referenced by"

    Plan {
        guid id PK
        string name
        int tier
        string_array editions
        string_array extensions
        jsonb limits
        decimal base_price
        string currency
        string billing_cycle
        bool is_active
    }
    NmpSubscription {
        guid id PK
        guid tenant_id
        guid plan_id FK
        string status
        datetime trial_start
        datetime trial_end
        datetime current_period_start
        datetime current_period_end
        bool cancel_at_period_end
        string payment_provider
        string provider_subscription_id
    }
    Entitlement {
        guid tenant_id PK
        int tier
        string_array editions
        string_array extensions
        jsonb limits
        datetime expires_at
        timestamp? grace_until
        long generation
    }
    NmpInvoice {
        guid id PK
        guid subscription_id FK
        guid tenant_id
        string status
        decimal subtotal
        decimal tax
        decimal total
        string currency
        datetime issued_at
        datetime due_at
        datetime paid_at
        string provider_invoice_id
    }
    NmpInvoiceLine {
        guid id PK
        guid invoice_id FK
        string description
        int quantity
        decimal unit_price
        decimal amount
        string proration_ref
    }
    WebhookLedger {
        guid id PK
        string provider_name
        string provider_event_id
        string event_type
        jsonb raw_payload
        datetime received_at
        datetime processed_at
        string processing_status
    }
```

### Entitlement refresh rule

- On any `NmpSubscription` state change (create, upgrade, downgrade, cancel, renew, trial
  transition, payment-failed dunning) or underlying `Plan` change, NMP recomputes the
  tenant's `Entitlement` row and increments `generation` monotonically.
- A `tenant.entitlement.updated` integration event is published via Dapr pub/sub carrying
  `{tenantId, generation, expiresAt}`.
- The SaaS runtime (`NmpLicenseVerifier`) caches the `Entitlement` for up to 15 minutes via
  `ICacheService`. Beyond 15 minutes it re-fetches lazily.
- On `tenant.entitlement.updated` event receipt, the runtime invalidates the cached
  entitlement for that tenant immediately (eager invalidation; ADR-0013).
- On-prem tenants cache the `Entitlement` embedded in the RSA-signed license key; the
  phone-home probe (daily by default, configurable per deployment) refreshes the key and the
  cached projection. Within `grace_until` after `expires_at` the tenant continues to run in
  degraded mode; beyond `grace_until` modules requiring a valid entitlement go read-only.

### License-verify API contract

```
POST /internal/license/verify
Body:     { tenantId: Guid, moduleId: string }
Response: {
  allowed:    bool,
  tier:       int,
  editions:   string[],
  extensions: string[],
  limits:     { users: int, orgs: int, api_rpm: int, storage_gb: int },
  expiresAt:  DateTime,
  graceUntil: DateTime?,
  generation: int
}
```

`allowed` is `true` iff (a) `moduleId` is covered by `tier`, `editions`, or `extensions`,
(b) `now < expiresAt` OR `now < graceUntil`, and (c) the tenant is not suspended. The
response is the **only** shape `NmpLicenseVerifier` exposes to module code; callers never see
`Plan` or `NmpSubscription`.

### Stripe / iyzico mapping

- `NmpSubscription.provider_subscription_id` is the authoritative external ID from
  Stripe (`sub_…`) or iyzico (subscription reference).
- Invoice pairing between NMP and the provider happens through `WebhookLedger`: on each
  webhook, NMP dedupes by `(provider_name, provider_event_id)` before applying effects. The
  ledger is append-only and serves replay and audit.
- Both providers sit behind `IPaymentProvider` (ADR-0018); NMP never imports a provider SDK
  type into its domain.

## Consequences

### Positive

- SaaS runtime license check is O(1) in the happy path (cache hit) and bounded O(1) on cache
  miss (single NMP roundtrip); no multi-table join on the hot path.
- Air-gapped and on-prem tenants work from the signed license key with bounded, predictable
  refresh semantics.
- `WebhookLedger` gives full audit trail and deterministic replay across Stripe and iyzico.
- Clear separation: operators configure `Plan`, tenants hold `NmpSubscription`, runtime sees
  only `Entitlement`.

### Negative

- Adds a second recurring engine inside the NMP codebase, raising the total to three across
  the platform (Subscription tenant-side, Fundraising NGO, NMP platform-side). Shared
  `IPaymentProvider` absorbs more load and more provider-quirk coverage; this must be
  explicitly documented in the payment-port spec.
- `Entitlement` table must be kept in sync with `Plan` and `NmpSubscription`; any recompute
  path missed is a correctness bug. Mitigated by a single recompute helper invoked from all
  state-change handlers and an integration test enforcing that invariant.
- Two-currency-domain reporting (operator currency vs. tenant currency) is non-trivial; see
  TODO below.

### Neutral

- The existing Phase 1 `NullLicenseVerifier` remains the default in `Development` only; from
  NMP.2 onward, Staging and Production wire up the real verifier pointed at NMP.
- Introduces the `tenant.entitlement.updated` integration event as a new cross-codebase
  contract (NMP → SaaS).

## Implementation notes

- `Plan` is operator-configured via the NMP admin UI; no auto-provisioning from the tenant
  side. Only NMP operators can create or modify plans.
- `Entitlement` is the **only** projection `NmpLicenseVerifier` returns; the SaaS runtime
  never sees `Plan` or `NmpSubscription` shapes directly.
- Architecture test: `Nexora.Management.*` namespaces MUST NOT be referenced from
  `Nexora.Host` or any `Nexora.Modules.*` project. Integration is HTTPS-only via the
  `/internal/license/verify` endpoint and the `tenant.entitlement.updated` pub/sub topic.
- Entitlement cache: 15-minute TTL via `ICacheService`, key
  `nmp:entitlement:{tenantId}` (tenant prefix auto-applied by `DaprCacheService`).
- Webhook idempotency: enforce unique `(provider_name, provider_event_id)` index on
  `WebhookLedger`; processing handler upserts on conflict and no-ops on duplicates.
- On-prem license key: RSA-2048 signed; embed `Entitlement` projection + `issued_at` +
  `expires_at` + `grace_until` + `generation`. Phone-home probe is a daily Hangfire recurring
  job (`nmp:onprem-phonehome`) per on-prem deployment.
- **Proration policy:** When a tenant upgrades mid-cycle, the remaining days on the current plan are credited at the daily rate (`plan_price / days_in_billing_period`) and the new plan is charged in full from the upgrade date. Downgrades take effect at the next billing cycle start — no mid-cycle proration on downgrades. Proration is calculated in the NMP reporting currency (see below). Fractional cents are rounded up (ceiling) in favor of NMP.
- **NMP reporting currency:** USD. All plan prices are stored and invoiced in USD. When a tenant's local currency differs, the display layer converts using the daily exchange rate from `IExchangeRateService` (ADR-0021) for UI purposes only — the canonical invoice amount is always USD. Multi-currency billing (invoicing in tenant's local currency) is out of scope for NMP.1 and NMP.2; tracked as a future NMP.3 enhancement.

## References

- ADR-0013 — Cache cross-instance invalidation.
- ADR-0016 — Module tier classification.
- ADR-0017 — Portal extension architecture.
- ADR-0018 — Payment provider strategy (shared `IPaymentProvider` port).
- ADR-0019 — Recurring billing vs. recurring donations (three-engine separation pattern).
- ADR-0021 — Money and exchange-rate contract.
- `docs/roadmap/phases/phase-NMP-track.md` — NMP milestone plan.
- `docs/architecture/MANAGEMENT_PORTAL.md` — NMP architecture overview.
