# 0021 — Money value object and exchange-rate contract

## Status

Accepted

## Date

2026-04-22

## Context

Multiple modules across the Nexora platform — Finance, Subscription, Fundraising, HR, and
Projects — all deal with monetary amounts and routinely need to convert between currencies.
During early Phase-1 work each module introduced its own ad-hoc shape for representing money
(raw `decimal` in some DTOs, `decimal + string` tuples in events, one-off `Money` records in a
module's own Domain namespace) and fetched exchange rates from whichever source was convenient
at the time.

Agent B's review (Prompt 2) flagged this as a cross-cutting inconsistency: there is no single
contract that pins down the shape of a monetary value at module boundaries, and no designated
owner for exchange-rate data. The operational rules for rounding, daily snapshots, and
display formatting already live in `docs/standards/multi-currency.md`, but the *interface*
contract — the shape of types, the ownership of the rate service, the event payload rule — is
not captured anywhere authoritative.

This ADR formalizes that interface contract so no new module can invent its own `Money` or
rate-fetch path. It aligns with:

- `docs/standards/multi-currency.md` (operational rules: rounding, providers, display).
- `docs/decisions/0014-distributed-consistency-patterns.md` (idempotency framing for cached
  conversions and daily snapshots).
- `docs/decisions/0016-module-tier-classification.md` (Finance is a Tier-2 Enterprise module
  and is the natural owner of the rate service).
- `docs/modules/tier-2-enterprise/finance/SPEC.md` (Finance as source-of-truth for rates).
- `docs/modules/tier-2-enterprise/subscription/SPEC.md` (representative consumer).

## Decision drivers

- Must give all modules a single, stable monetary type at boundaries (DTOs, events, domain
  entities) so cross-module payloads are self-describing.
- Must name a single tenant-scoped owner for exchange-rate data to preserve auditability and
  avoid duplicated provider integrations.
- Must preserve tenant isolation — rates and conversions are tenant-scoped like all Finance
  data.
- Must not regress precision for currencies with more than two fractional digits (e.g. BHD)
  or for rounding-safe intermediate math.
- Must remain consistent with the operational rules already published in
  `standards/multi-currency.md` (banker's rounding, daily 06:00 UTC refresh, TCMB/ECB
  providers, `Intl.NumberFormat` on the frontend).
- Must stay within the modular-monolith envelope — no new microservice solely for rates.
- Must allow modules to be unit-tested without a real Finance installation.

## Considered options

1. **Option A — SharedKernel `Money` + Finance-owned `IExchangeRateService`** *(chosen)*
   - Summary: Introduce `Money { decimal Amount, string CurrencyCode }` in
     `Nexora.SharedKernel.ValueObjects` as the only monetary type at module boundaries.
     Define `IExchangeRateService` in SharedKernel; implement it in Finance, which owns the
     `exchange_rates` table, the daily refresh job, and the audit trail.
   - Pros: Single contract eliminates per-module drift. Events become self-describing
     (`Amount + CurrencyCode`). Finance retains its natural role as the auditable source of
     rates. Cheap to evolve — no network boundary added. Test doubles are trivial.
   - Cons: Modules running without Finance installed need a fake/in-memory provider for
     tests. Cross-currency flows hard-block when Finance is not installed for a tenant (see
     Consequences — this is considered acceptable).

2. **Option B — Each module defines its own `Money` and its own rate fetch**
   - Summary: Let each module ship its own `Money` record in its Domain layer and call
     whichever provider it wants.
   - Pros: Zero cross-module coupling; modules can be removed cleanly.
   - Cons: Duplicates logic and validation. Inconsistent rounding across modules. No single
     audit trail for what rate a given posted record used. Events carrying money become
     ambiguous across producer/consumer pairs. Rejected.

3. **Option C — Dedicated rates microservice**
   - Summary: Extract exchange-rate ownership into its own service with its own API.
   - Pros: Theoretically reusable by future non-Nexora clients; clean separation.
   - Cons: Over-engineering for a modular monolith at Phase-1 scale. Adds a network
     boundary, deploy target, and SLO to track. Finance already owns the General Ledger and
     is where auditors expect rate history to live. Rejected for now; may be revisited if
     rates ever need to be shared beyond Nexora.

## Decision outcome

Option A is chosen. `Money` becomes a SharedKernel value object used by every module at its
boundaries, and Finance is the single tenant-scoped source of truth for exchange rates,
exposed to other modules only through `IExchangeRateService` defined in SharedKernel.

Rationale tied to the drivers: Option A gives every module the same monetary shape (driver 1)
with a single named owner (driver 2), keeps rate data tenant-scoped under the existing
Finance boundary (driver 3), uses `decimal(18,4)` storage precision to cover BHD and safe
intermediate math (driver 4), and inherits the already-published operational rules from
`standards/multi-currency.md` (driver 5). It adds no new service (driver 6) and makes
test-double injection straightforward via the interface in SharedKernel (driver 7). Options
B and C fail drivers 2 and 6 respectively.

Concretely, the decision pins down:

1. **Type at boundaries.** `Money { decimal Amount, string CurrencyCode }` in
   `Nexora.SharedKernel.ValueObjects` is the only permitted monetary type in domain
   entities, DTOs, and integration events. Raw `decimal` is permitted ONLY inside a module's
   private computation helpers, never at module boundaries.
2. **CurrencyCode rules.** ISO-4217, three-letter, uppercase; validated at `Money`
   construction.
3. **Storage precision.** `Amount` persists as `decimal(18,4)`; four fractional digits
   accommodate currencies like BHD and keep intermediate math rounding-safe.
4. **Display rounding.** Follows `standards/multi-currency.md` — banker's rounding for
   financial totals, `Intl.NumberFormat` on the frontend.
5. **Exchange-rate ownership.** Finance module is the single tenant-scoped source of truth
   for rates. Other modules MUST NOT fetch rates directly; they call `IExchangeRateService`
   from SharedKernel. The interface is defined here; the implementation lives in Finance.
6. **Interface shape.**

   ```csharp
   public interface IExchangeRateService
   {
       Task<ExchangeRate> GetRateAsync(string from, string to, DateOnly asOf, CancellationToken ct);
       Task<Money> ConvertAsync(Money source, string toCurrency, DateOnly asOf, CancellationToken ct);
   }

   public sealed record ExchangeRate(string From, string To, decimal Rate, DateOnly AsOf, string Source);
   ```

7. **Refresh cadence and providers.** Rates are loaded daily at 06:00 UTC — TCMB for
   TRY-base pairs, ECB for EUR-base pairs, with a configurable fallback provider for other
   currencies. If Finance is not installed for a tenant, the SharedKernel interface's
   fallback implementation uses a "no-conversion" policy: pass-through for same-currency
   calls and `throw new CurrencyConversionUnavailable(...)` for any mismatch.
8. **Event payloads.** All cross-module integration events carrying money MUST embed
   `Amount + CurrencyCode` (i.e. the raw `Money`) and MUST NOT pre-convert to a base
   currency. The consumer decides whether and when to convert.
9. **Idempotency.** Conversions are pure given `(from, to, asOf)` and are therefore
   cache-safe. Finance stores daily snapshots; mid-day rate movements do not retroactively
   rewrite already-posted records. This is consistent with the idempotency framing in
   ADR-0014.

## Consequences

### Positive

- Single contract across modules eliminates per-module drift and ambiguous event payloads.
- Finance remains the auditable source of rates; auditors have one table to inspect.
- Integration events become self-describing — no hidden "what base currency was this in?"
  context.
- Architecture tests can mechanically enforce the rule that no module outside SharedKernel
  and Finance declares its own `Money` or `ExchangeRate` type.
- Test ergonomics: the interface lives in SharedKernel so any module can inject an
  in-memory fake without referencing Finance.

### Negative

- Modules not dependent on Finance (especially in unit and contract tests) must ship or
  consume a fake/in-memory `IExchangeRateService`; guidance for this belongs in
  `standards/testing.md`.
- If a tenant uninstalls Finance, cross-currency flows hard-block (the fallback throws
  `CurrencyConversionUnavailable`). This is explicitly acceptable: multi-currency business
  activity already requires Finance for GL posting, so a tenant without Finance shouldn't
  be running cross-currency flows in the first place.
- Finance owns a new operational responsibility (the daily refresh job and the provider
  integrations), which must be monitored like any other scheduled job.

### Neutral

- Several existing Phase-1 modules already use an informal `Money` shape; this ADR
  formalizes rather than introduces. A one-time sweep is needed to move those types into
  SharedKernel and delete the duplicates.
- The `exchange_rates` table becomes a new Finance-owned artifact, but it fits cleanly into
  the Finance schema alongside GL tables and does not introduce a new data store.

## Implementation notes

- **Packages / modules affected:**
  - `Nexora.SharedKernel` — add `ValueObjects.Money`, `IExchangeRateService`,
    `ExchangeRate` record, and `CurrencyConversionUnavailable` exception.
  - `Nexora.Modules.Finance` — implement `ExchangeRateService` (EF-backed), the
    `exchange_rates` table + migration, the daily 06:00 UTC Hangfire job, and provider
    adapters (TCMB, ECB, configurable fallback).
  - All other modules — replace any local `Money`/rate types with the SharedKernel ones.

- **`exchange_rates` table shape (Finance schema):**
  - Primary key: (`from`, `to`, `as_of`).
  - Columns: `rate decimal`, `source text`, `fetched_at timestamptz`.
  - Tenant-scoped via schema-per-tenant as with all Finance tables.

- **Validation:**
  - `Money` constructor validates ISO-4217 on `CurrencyCode` and rejects negative
    `Amount` only where the caller opts in (e.g. strict-totals mode); signed amounts remain
    legal because refunds and reversals need them.
  - `CurrencyCode` is normalized to uppercase at construction.

- **Rollout plan:**
  - Phase 1 — land the SharedKernel types and the Finance implementation behind an
    architecture test that fails if any module outside SharedKernel/Finance declares
    `Money` or `ExchangeRate`.
  - Phase 2 — sweep existing modules to use the SharedKernel types; update their DTOs,
    events, and storage mappings.
  - Phase 3 — enable the architecture test in CI.

- **Observability:**
  - Metric: `finance_exchange_rate_refresh_duration_ms` histogram (labels: `provider`).
  - Metric: `finance_exchange_rate_refresh_failures_total` counter (labels: `provider`).
  - Log: `Information` on successful daily refresh with row count; `Warning` on provider
    fallback; `Error` on total refresh failure.

- **Testing:**
  - Architecture test: no project outside `Nexora.SharedKernel` and
    `Nexora.Modules.Finance` may declare a type named `Money` or `ExchangeRate`.
  - Unit tests for `Money` construction (ISO-4217 validation, case normalization,
    equality).
  - Contract tests for `IExchangeRateService` pass-through behaviour when Finance is not
    installed.
  - Integration test: daily refresh job writes a snapshot; a later mid-day rate change does
    not alter an already-posted record's rate.

- **Maintainer-resolved items (see Amendment 1 — 2026-04-22):**
  - Fallback provider for non-TR, non-EU currencies is OpenExchangeRates; see Amendment 1 (A1.1).
  - Default `asOf` when the caller omits a date is `DateOnly.FromDateTime(DateTime.UtcNow)`; see Amendment 1 (A1.2).

## References

- `docs/standards/multi-currency.md`
- `docs/decisions/0014-distributed-consistency-patterns.md`
- `docs/decisions/0016-module-tier-classification.md`
- `docs/modules/tier-2-enterprise/finance/SPEC.md`
- `docs/modules/tier-2-enterprise/subscription/SPEC.md`

---

## Amendment 1 — 2026-04-22

Resolves the two maintainer TODOs from the original Implementation notes.

### A1.1 Fallback exchange-rate provider

**Decision:** OpenExchangeRates is the sanctioned fallback for currency pairs not covered by
TCMB (TRY base) or ECB (EUR base), and as a redundant source when a primary is unreachable.

**Rationale:** frankfurter.app is ECB-sourced and limited to ~30 major currencies
(Europe-centric). Nexora's SaaS customers span TR / EU / US plus longer-tail pairs
(GBP, AED, JPY, AUD, CAD, CHF), and OpenExchangeRates supports ~170 currencies. The free tier
(1,000 calls/month) is sufficient: the daily snapshot job makes one call per base currency per
day (≤ 3 calls/day for TRY/EUR/USD bases → ~90/month), well inside the quota. TCMB remains
primary for TRY pairs and ECB remains primary for EUR pairs; OpenExchangeRates is used as the
fallback when either primary is unreachable or when the requested pair falls outside the
primary's coverage.

**Configuration:**
- API key via `ISecretProvider`: `nexora/exchange-rate/openexchangerates/api-key`.
- Provider ordering per base currency:
  - `TRY` → TCMB (primary) → OpenExchangeRates (fallback)
  - `EUR` → ECB (primary) → OpenExchangeRates (fallback)
  - everything else → OpenExchangeRates (primary)
- A provider outage flips the next daily snapshot job to the next ranked provider; the
  `exchange_rates.source` column captures which provider the stored rate came from.

### A1.2 Default `asOf` policy

**Decision:** When the caller passes `default(DateOnly)` or omits the parameter,
`IExchangeRateService` substitutes `DateOnly.FromDateTime(DateTime.UtcNow)`. Historical
conversions require an explicit date; there is no implicit "latest-ever".

**Rationale:** Deterministic behaviour for event handlers. Tests can pin `asOf` without
relying on a clock. Callers that genuinely need today's rate book continue to work with
a zero-argument call.

**Handler rule:** event-driven conversions (e.g. Finance consuming `Subscription.InvoicePaid`)
MUST pass the business date carried in the event payload, not `UtcNow`, so replays produce
identical GL entries.
